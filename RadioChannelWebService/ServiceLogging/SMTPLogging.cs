using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Reflection; 
using System.Text;
using System.Threading;

namespace RadioChannelWebService
{
    /// <summary>
    /// Class voor het loggen van usage regels (net zoals apache) 
    /// 
    /// Let op vanwege thread stopt applicatie niet automatisch en wordt dus ook niet
    /// de destructor hier aangeroepen. Dus altijd bij afsluiten app SMTPLogging.Close() aanroepen in WindowsService.OnStop
    /// (niet Dispose want daar kun je niet bij omdat de instance private is!)
    /// </summary>
    public class SMTPLogging: IDisposable
    {
        private static bool disposed = false;
        private static SMTPLogging _singleton = null;

        private static string SMTPServer = "smtp.example.com";
        private static string EMail_Automatisering = "example@example.com";

        private static Thread logThread = null;
        private static bool logThreadStarted = false;
        public static EventWaitHandle waitEvent = new EventWaitHandle(false, EventResetMode.AutoReset);

        private static Queue<LogSMTPMessage> logRows;
        private static string AppName = System.IO.Path.GetFileName(System.IO.Path.ChangeExtension(Assembly.GetExecutingAssembly().Location, null));

        /// <summary>
        /// static constructor die private class initieert zodat destructor werkt (static destructor werkt blijkbaar niet)
        /// </summary>
        static SMTPLogging()
        {
            string IniPath = CDR.DB_Helper.FingerprintIniFile;
            if (System.IO.File.Exists(IniPath))
            {
                try
                {
                    CDR.Ini.IniFile ini = new CDR.Ini.IniFile(IniPath);
                    EMail_Automatisering = ini.IniReadValue("Mail", "EMail", EMail_Automatisering);
                    SMTPServer = ini.IniReadValue("Mail", "SMTPServer", SMTPServer);
                }
                catch { }
            }

            logRows = new Queue<LogSMTPMessage>();
            _singleton = new SMTPLogging();
        }

        private SMTPLogging()
        {            
            CreateLogThread();
        }

        #region IDispose implementation
        static public void Close()
        {
            if (_singleton != null)
            {
                _singleton.Dispose();
            }
        }

        /// <summary>
        /// Implement IDisposable.
        /// Do not make this method virtual.
        /// A derived class should not be able to override this method.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            // This object will be cleaned up by the Dispose method.
            // Therefore, you should call GC.SupressFinalize to
            // take this object off the finalization queue 
            // and prevent finalization code for this object
            // from executing a second time.
            GC.SuppressFinalize(_singleton);
        }

        /// <summary>
        /// Dispose(bool disposing) executes in two distinct scenarios.
        /// If disposing equals true, the method has been called directly
        /// or indirectly by a user's code. Managed and unmanaged resources
        /// can be disposed.
        /// If disposing equals false, the method has been called by the 
        /// runtime from inside the finalizer and you should not reference 
        /// other objects. Only unmanaged resources can be disposed.
        /// </summary>
        /// <param name="disposing"></param>
        protected void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    KillLogThread();
                    logRows.Clear();
                }
            }
            disposed = true;
        }

        /// <summary>
        /// Use C# destructor syntax for finalization code.
        /// This destructor will run only if the Dispose method 
        /// does not get called.
        /// It gives your base class the opportunity to finalize.
        /// Do not provide destructors in types derived from this class.
        /// </summary>
        ~SMTPLogging()
        {
            Dispose(false);
        }
        #endregion

        private void CreateLogThread()
        {
            if (logThread != null)
            {
                KillLogThread();
            }

            logThreadStarted = false;

            // Start de thread
            ThreadStart st = new ThreadStart(ExecuteLogTask); // create a thread and attach to the object
            logThread = new Thread(st);
            logThread.Start();
        }

        private void KillLogThread()
        {
            try
            {
                if (logThread != null)
                {
                    // Stop de thread's
                    if (logThreadStarted)
                    {
                        logThreadStarted = false;

                        // Maak thread wakker
                        waitEvent.Set(); // start de thread mocht de thread in een slaap toestand staan
                        // Wacht max 1 minuut om het ding te laten stoppen
                        logThread.Join(new TimeSpan(0, 0, 10));
                    }
                    logThread.Abort();
                    logThread = null;
                }
            }
            catch { }
        }

        /// <summary>
        /// Functie die in een thread de log regels parallel naar disk schrijft.
        /// De andere modules hoeven hierdoor niet te wachten tot de regels naar disk is geschreven
        /// </summary>
        public void ExecuteLogTask()
        {
            logThreadStarted = true;
            try
            {
                // De worker thread
                DateTime lastStatusMail = DateTime.MinValue;
                while (logThreadStarted)
                {
                    try
                    {
                        // Aantal tellen hoeft niet via een lock. Pas als we gaan verwijderen moeten we de List locken!
                        int logCount = logRows.Count;
                        if (logCount > 0)
                        {
                            // Filenaam bepalen waar we naartoe moeten schrijven
                            string filename = System.IO.Path.GetFullPath(string.Format(@".\Log\{0}-{1:yyyyMMdd}-access.log", AppName, DateTime.Now));
                            if (!Directory.Exists(Path.GetDirectoryName(filename)))
                            {
                                // Zorg dat path bestaat
                                Directory.CreateDirectory(Path.GetDirectoryName(filename));
                            }

                            // Foreach kan niet gebruikt worden omdat we elementen willen verwijderen
                            // EN het toevoegen niet willen blokkeren (door complete foreach in een lock te gooien)
                            for (int i = 0; i < logCount; i++)
                            {
                                LogSMTPMessage? msg;
                                lock (logRows)
                                {
                                    try
                                    {
                                        msg = logRows.Dequeue();
                                    }
                                    catch
                                    {
                                        msg = null;
                                    }
                                } //lock

                                // Nu message naar smtp server versturen
                                if (msg != null)
                                {
                                    // Van alle fouten (oa disk full) af
                                    try
                                    {                                               
                                        SendMail(((LogSMTPMessage)msg).To, ((LogSMTPMessage)msg).Subject, ((LogSMTPMessage)msg).Message);
                                    }
                                    catch { }
                                }
                            } // for
                        }
                    }
                    catch { } // gooi foutmelding weg en probeer het over 1 seconden nog eens

                    if (logThreadStarted)
                    {
                        waitEvent.WaitOne(1000, false); // na 1 seconden gaan we weer kijken of we logs moeten wegschrijven
                    }
                } //while
            }
            finally
            {
                logThreadStarted = false;
                Thread.CurrentThread.Abort();
            }
        }

        private static bool SendMail(string toEmailAdress, string subject, string message)
        {
            try
            {
                SmtpClient smtpClient = new SmtpClient();

                smtpClient.Host = SMTPServer;
                smtpClient.Timeout = 30 * 1000; // 30 seconden timeout max

                string AppName = System.IO.Path.GetFileName(System.IO.Path.ChangeExtension(Assembly.GetExecutingAssembly().Location, null));

                // En verstuur het (hopelijk geen firewall ofzo)
                smtpClient.Send(AppName + " <" + EMail_Automatisering + ">", toEmailAdress, subject, message);

                return true;
            }
            catch { }

            return false;
        }

        public static void AddSMTPMessage(string to, string subject, string message)
        {
            LogSMTPMessage msg = new LogSMTPMessage();
            msg.To = to;
            msg.Subject = subject;
            msg.Message = message;

            lock (logRows)
            {
                // Probeer logging beetje te temperen
                if (logRows.Count <= 2)
                {
                    logRows.Enqueue(msg);
                }
            }
        }

        public static void AddSMTPMessage(string subject, string message)
        {
            LogSMTPMessage msg = new LogSMTPMessage();
            msg.To = EMail_Automatisering;
            msg.Subject = subject;
            msg.Message = message;

            lock (logRows)
            {
                // Probeer logging beetje te temperen
                if (logRows.Count <= 2)
                {
                    logRows.Enqueue(msg);
                }
            }
        }

        public struct LogSMTPMessage
        {
            public string To;
            public string Subject;
            public string Message;
        }
    }
}
