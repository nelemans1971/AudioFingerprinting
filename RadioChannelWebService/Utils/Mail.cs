using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace RadioChannelWebService
{
    static class Mail
    {
        private static bool Initialized = false;
        public static string SMTPServer = "smtp.example.com";
        public static string EMail_Automatisering = "example@example.com";


        private static void Initialize()
        {
            if (!Initialized)
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
                Initialized = true;
            }
        }


        static public bool SendMail(string toEmailAdress, string subject, string message)
        {
            Initialize();

            try
            {
                SmtpClient smtpClient = new SmtpClient();

                smtpClient.Host = SMTPServer;
                smtpClient.Timeout = 30 * 1000; // 30 seconden timeout max

                string AppName = System.IO.Path.GetFileName(System.IO.Path.ChangeExtension(Assembly.GetExecutingAssembly().Location, null));

                // En verstuur het (hopelijk geen firewall of 
                smtpClient.Send(AppName + " <" + EMail_Automatisering + ">", toEmailAdress, subject, message);

                return true;
            }
            catch
            {
            }

            return false;
        }
    }
}
