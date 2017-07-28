using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Mail;
using System.Reflection;
using System.Security.Principal;
using System.Text;
using BitFactory.Logging;
using Microsoft.Win32;

namespace CDR.Logging
{
    /// <summary>
    /// Deze class verzorgt het loggen. Als de applicatie in DEBUG wordt aangemaakt wordt er zowel een
    /// filelogger als een memorylogger aangemaakt. Alle logboodschappen gaan naar beide logger.
    /// De log file staat in de map waar ook de applicatie executable staat. De berichten worden altijd geAppened.
    /// Voor de Memory logger geldt dat er maximaal 1000 regels gelogd worden. Daarna worden de oudste overschreven.
    /// </summary>
    static class CDRLogger
    {
        public static string SMTPServer = "smtp.example.com";
        public static string EMail_Automatisering = "example@example.com";

        public static CompositeLogger Logger;

        private static string filename = @".\\log.log";
        private static FileLogger fileLogger = null;
        private static MemoryLogger memoryLogger = null;


        static CDRLogger()
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

            // Gebruik eigen formatter
            BitFactory.Logging.Logger.DefaultFormatterClass = typeof(LogEntryCDRFormatter);

            // We gebruiken de application naam als filename voor het log bestand.
            filename = String.Format(".\\{0}.log", System.Reflection.Assembly.GetExecutingAssembly().GetName().Name);
            if (File.Exists(filename))
            {
                // We voegen info toe om te vertellen dat dit een nieuwe restart is van de applicatie
                using (StreamWriter w = File.AppendText(filename))
                {
                    w.WriteLine();
                    w.WriteLine(String.Format("Application started at {0:yyyy-MM-dd HH:mm:ss.fff}", DateTime.Now));
                    w.WriteLine();
                } //using
            } //if file exists

            Logger = new CompositeLogger();
            // voeg altijd memory logger toe
            memoryLogger = new MemoryLogger(1000); // capacity van 1000 regels maximaal daarna worden de oude overschreven
            Logger.AddLogger("MemoryLogger", memoryLogger);

            fileLogger = new FileLogger(filename);
            Logger.AddLogger("FileLogger", fileLogger);
        }

        /// <summary>
        /// Als je bij de specifieke filelogger wil komen. Zou niet nodig moeten zijn.
        /// </summary>
        static public FileLogger FileLogger
        {
            get
            {
                return fileLogger;
            }
        }

        /// <summary>
        /// Als je bij de memory logger moet zijn. BV als je de logentries wil uitlezen.
        /// </summary>
        static public MemoryLogger MemoryLogger
        {
            get
            {
                return memoryLogger;
            }
        }

        static private string GetIEVersion()
        {
            RegistryKey rk;
            string version;

            // IE 7
            try
            {
                rk = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Internet Explorer");
                version = rk.GetValue("Version").ToString();
            }
            catch
            {
                try
                {
                    // IE4+
                    rk = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Internet Explorer\Version Vector");
                    version = rk.GetValue("IE").ToString();
                }
                catch
                {
                    version = "Unknown";
                }
            }

            return version;
        }

        static public string SystemSettings()
        {
            StringBuilder info = new StringBuilder(1024);
            try
            {
                info.Append(String.Format("Machine Name: {0}\r\n", Environment.MachineName));
                info.Append(String.Format("Current OS: {0}\r\n", Environment.OSVersion));
                info.Append(String.Format(".NET Version: {0}\r\n", Environment.Version));
                info.Append(String.Format("IE version: {0}\r\n", GetIEVersion()));                
                info.Append(String.Format("Command Line: {0}\r\n", Environment.CommandLine));
                info.Append(String.Format("Current Directory: {0}\r\n", Environment.CurrentDirectory));
                info.Append(String.Format("System Directory: {0}\r\n", Environment.SystemDirectory));
                info.Append(String.Format("Working Set: {0}\r\n", Environment.WorkingSet));

                info.Append("\r\nUserinfo\r\n");
                info.Append(String.Format("DomainName/User: {0}/{1}\r\n", Environment.UserDomainName, Environment.UserName));

                WindowsIdentity user = WindowsIdentity.GetCurrent();
                WindowsPrincipal princ = new WindowsPrincipal(user);
                info.Append(String.Format("Is Administrator = {0}\r\n", princ.IsInRole(WindowsBuiltInRole.Administrator)));
                info.Append(String.Format("Is PowerUser = {0}\r\n", princ.IsInRole(WindowsBuiltInRole.PowerUser)));
                info.Append(String.Format("Is User = {0}\r\n", princ.IsInRole(WindowsBuiltInRole.User)));
                info.Append(String.Format("Is Guest = {0}\r\n", princ.IsInRole(WindowsBuiltInRole.Guest)));

                info.Append("\r\nCulture info\r\n");

                System.Globalization.CultureInfo cultureInfo = System.Globalization.CultureInfo.CurrentCulture;
                info.Append(String.Format("ShortDatePattern: {0}\r\n", cultureInfo.DateTimeFormat.ShortDatePattern));
                info.Append(String.Format("LongDatePattern: {0}\r\n", cultureInfo.DateTimeFormat.LongDatePattern));
                info.Append(String.Format("DateSeparator: {0}\r\n", cultureInfo.DateTimeFormat.DateSeparator));
                info.Append(String.Format("DisplayName: {0}\r\n", cultureInfo.DisplayName));
                info.Append(String.Format("NumberDecimalSeparator: {0}\r\n", cultureInfo.NumberFormat.NumberDecimalSeparator));

                info.Append("\r\n\r\n");
            }
            catch (Exception e)
            {
                info.Append("CDRLogger.SystemSettings generated an exception\r\n");
                info.Append(e.Message + "\r\n");
            }

            return info.ToString();
        }

        /// <summary>
        /// Verstuur de log naar de mailserver van de CDR
        /// </summary>
        /// <param name="fromEmailAdress"></param>
        /// <param name="extraMessage"></param>
        /// <returns></returns>
        static public bool SendSmtpOfMemoryLog(string toEmailAdress, string extraMessage)
        {
            try
            {
                if (memoryLogger != null)
                {
                    SmtpClient smtpClient = new SmtpClient();

                    smtpClient.Host = SMTPServer;
                    smtpClient.Timeout = 30 * 1000; // 30 seconden timeout max

                    LogEntry[] logEntries = memoryLogger.GetLog();
                    StringBuilder message = new StringBuilder(logEntries.Length * 1024);

                    if (extraMessage != null && extraMessage.Length > 0)
                    {
                        message.Append(extraMessage);
                        message.Append("\r\n\r\n------------------------------------------------------------\r\n\r\n");
                    }

                    message.Append(SystemSettings());

                    foreach (LogEntry log in logEntries)
                    {
                        message.Append(memoryLogger.Formatter.AsString(log) + "\r\n");
                    } //foreach

                    string AppName = Path.GetFileName(Path.ChangeExtension(Assembly.GetExecutingAssembly().Location, null));

                    // En verstuur het (hopelijk geen firewall of 
                    smtpClient.Send(EMail_Automatisering, toEmailAdress, AppName + " log", message.ToString());
                    //smtpClient.Send(fromEmailAdress, "yvo@cdr.nl", "Digileen 2.0 Log", message.ToString());

                    return true;
                }
            }
            catch { }

            return false;
        }

        static public void WriteToConsole()
        {
            LogEntry[] logEntries = memoryLogger.GetLog();
            foreach (LogEntry log in logEntries)
            {
                
                Console.WriteLine(memoryLogger.Formatter.AsString(log));
            } //foreach
        }

        /// <summary>
        /// Verstuur de log naar de mailserver van de CDR
        /// </summary>
        /// <param name="fromEmailAdress"></param>
        /// <returns></returns>
        static public bool SendSmtpOfMemoryLog(string toEmailAdress) 
        {
            return  SendSmtpOfMemoryLog(toEmailAdress, null);
        }
    }
}
