using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection; 
using System.Text;
using System.Threading;

namespace RadioChannelWebService
{
    /// <summary>
    /// Class voor het loggen van usage regels (netzoals apache) 
    /// Let op vanwege thread stopt applicatie niet automatisch en wordt dus ook niet
    /// de destructor hier aangereopen. Dus altijd bij afsluiten app LogUsage.Close() aanroepen in WindowsService.OnStop
    /// (niet Dispose want daar kun je niet bij omdat de instance private is!)
    /// </summary>
    public class LogUsage: IDisposable
    {
        private static bool disposed = false;
        private static LogUsage _singleton = null;

        private static Thread logThread = null;
        private static bool logThreadStarted = false;
        public static EventWaitHandle waitEvent = new EventWaitHandle(false, EventResetMode.AutoReset);

        private static Queue<LogRow> logRows;

        private string AppName = System.IO.Path.GetFileName(System.IO.Path.ChangeExtension(Assembly.GetExecutingAssembly().Location, null));
        private DateTime? logDate = null;
        private StreamWriter swLogFile = null;

        /// <summary>
        /// static constructor die private class initieert zodat destructor werkt (static destructor werkt blijkbaar niet)
        /// </summary>
        static LogUsage()
        {
            logRows = new Queue<LogRow>();
            _singleton = new LogUsage();
        }

        private LogUsage()
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
        ~LogUsage()
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
                        logThread.Join(new TimeSpan(0, 1, 0));
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
            // YN 2011-11-09
            // Het open en sluiten van de logfile voor elke (paar) regels 
            // zorgt voor een extreem gefragmenteerde logfile. Deze is extreem traag bij openen
            // en kopieren. Nu houden we de file wel constant open, maar flushen we wel elke
            // seconden naar disk. Hopleijk lost dit het probleem op en kunnen we toch zien
            // wat er allemaal wordt gelogd elke minuut

            logThreadStarted = true;
            try
            {
                // De worker thread
                int flushCounter = 0;
                while (logThreadStarted)
                {
                    try
                    {
                        // Aantal tellen hoeft niet via een lock. Pas als we gaan verwijderen moeten we de List locken!
                        int logCount = logRows.Count;
                        if (logCount > 0)
                        {
                            // Foreach kan niet gebruikt worden omdat we elementen willen verwijderen
                            // EN het toevoegen niet willen blokkeren (door complete foreach in een lock te gooien)
                            for (int i = 0; i < logCount; i++)
                            {
                                LogRow? logRow;
                                lock (logRows)
                                {
                                    try
                                    {
                                        logRow = logRows.Dequeue();
                                    }
                                    catch
                                    {
                                        logRow = null;
                                    }
                                } //lock

                                // Nu regel naar logfile schrijven
                                if (logRow != null)
                                {
                                    if (swLogFile == null || logDate == null || ((DateTime)logDate - ((LogRow)logRow).TimeStamp.Date).Days != 0)
                                    {
                                        CloseLogFile();

                                        // Filenaam bepalen waar we naartoe moeten schrijven
                                        logDate = ((LogRow)logRow).TimeStamp.Date;
                                        string filename = System.IO.Path.GetFullPath(string.Format(@".\Log\{0}-{1:yyyyMMdd}-access.log", AppName, (DateTime)logDate));
                                        if (!Directory.Exists(Path.GetDirectoryName(filename)))
                                        {
                                            // Zorg dat path bestaat
                                            Directory.CreateDirectory(Path.GetDirectoryName(filename));
                                        }
                                        try
                                        {
                                            // Allow file to be opend by other files
                                            FileStream fs = new FileStream(filename, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read);
                                            fs.Seek(0, SeekOrigin.End);

                                            swLogFile = new StreamWriter(fs);
                                        }
                                        catch
                                        {
                                            swLogFile = null;
                                        }
                                    }

                                    // Vang alle fouten (oa disk full) af
                                    try
                                    {
                                        if (swLogFile != null)
                                        {
                                            swLogFile.WriteLine(((LogRow)logRow).LogLine);
                                        }
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
                        flushCounter++;
                        if (swLogFile != null && flushCounter >= 1)
                        {
                            // Zorg dat output op disk staat
                            swLogFile.Flush();
                            flushCounter = 0;
                        }
                    }
                                          
                    // Kunnen we de logfile afsluiten?
                    if (logDate != null && ((DateTime)logDate - System.DateTime.Now.Date).Days != 0)
                    {
                        CloseLogFile();
                    }
                } //while
            }
            finally
            {
                logThreadStarted = false;
                CloseLogFile();
                Thread.CurrentThread.Abort();
            }
        }

        private void CloseLogFile()
        {
            if (swLogFile != null)
            {
                swLogFile.Flush();
                swLogFile.Close();
                swLogFile = null;
                logDate = null;
            }
        }

        public static void AddLog(DateTime timestamp, string line)
        {
            LogRow logRow = new LogRow();
            logRow.TimeStamp = timestamp;
            logRow.LogLine = line;

            lock (logRows)
            {
                logRows.Enqueue(logRow);
            }
        }

        public static void AddLog(string line)
        {
            AddLog(DateTime.Now, line);
        }

        public struct LogRow
        {
            public DateTime TimeStamp;
            public string LogLine;
        }
    }
}
