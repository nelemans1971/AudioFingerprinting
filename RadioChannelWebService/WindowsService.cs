#region License
// Copyright (c) 2015-2017 Stichting Centrale Discotheek Rotterdam.
// 
// website: https://www.muziekweb.nl
// e-mail:  info@muziekweb.nl
//
// This code is under MIT licence, you can find the complete file here: 
// LICENSE.MIT
#endregion
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Install;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Reflection;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;
using System.ServiceModel.Web;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using BitFactory.Logging;
using CDR;
using CDR.Ini;
using CDR.Logging;
using System.ServiceModel.Channels;
using CDR.CaptureBehavior;

// http://www.codeproject.com/KB/cs/csharpwindowsserviceinst.aspx
namespace RadioChannelWebService
{
    // Class that represents the Service version of your app 
    public class WindowsService : ServiceBase
    {
        #region Standard stuff
        private static List<WindowsService> listWindowsService = new List<WindowsService>();

        private WebServiceHost host = null;
        private WebServiceHost hostHealthService = null;
        private System.Threading.Mutex serviceMutex = null;

        private Thread accountingThread;
        private bool accountingThreadStarted = false;
        private static EventWaitHandle waitEvent = new EventWaitHandle(false, EventResetMode.AutoReset);

        private static ReceiveIndex receiveIndex = null;


        public WindowsService()
        {
            listWindowsService.Add(this);

            this.ServiceName = Program.ServiceName;
        }

        ~WindowsService()
        {
            if (listWindowsService.Contains(this))
            {
                listWindowsService.Remove(this);
            }
        }

        /// <summary>
        /// Checks is a mutex is available which indicates this service is already running.
        /// </summary>
        public static bool ServiceIsRunningOnThisComputer
        {
            get
            {
                // Eerst controleren of we al niet draaien (vooral nodig voor commandline versie)
                string AppName = System.IO.Path.GetFileName(System.IO.Path.ChangeExtension(Assembly.GetExecutingAssembly().Location, null)) + PortNumber.ToString();
                bool createdNew = false;
                System.Threading.Mutex mutex = new System.Threading.Mutex(true, AppName, out createdNew);
                if (createdNew)
                {
                    // alleen als we hem zelf hebben gecreered vrij geven.
                    mutex.ReleaseMutex();
                }
                return !createdNew;
            }
        }

        public WebServiceHost Host
        {
            get
            {
                return host;
            }
        }
        #endregion

        #region Start/Stop of application handeling
        protected override void OnStart(string[] args)
        {
            int portNumber = PortNumber;

            // Eerst controleren of we al niet draaien (vooral nodig voor commandline versie)
            string AppName = System.IO.Path.GetFileName(System.IO.Path.ChangeExtension(Assembly.GetExecutingAssembly().Location, null)) + portNumber.ToString();
            bool serviceIsAlreadyRunningOnThisComputer = false;
            serviceMutex = new System.Threading.Mutex(true, AppName, out serviceIsAlreadyRunningOnThisComputer);
            if (serviceIsAlreadyRunningOnThisComputer)
            {
                serviceMutex = null;
                return;
            }

            // Force opening of indexs
            if (LuceneIndexes.IndexSubFingerprint.Index != null)
                ;


            // Run the service version here  
            //  NOTE: If you're task is long running as is with most  
            //  services you should be invoking it on Worker Thread  
            //  !!! don't take to long in this function !!! 
            base.OnStart(args);
            // http://www.leastprivilege.com/PermaLink.aspx?guid=b0ed39eb-01d9-4711-8d38-92d932e2e8c3
            // http://stackoverflow.com/questions/660445/basic-authentication-with-wcf-rest-service-to-something-other-than-windows-accoun
            // http://wcfrestcontrib.codeplex.com/

            // http://bytes.com/topic/net/answers/585032-wcf-username-authentication
            try
            {
                string uri = string.Format("http://{0}:{1}/", System.Net.Dns.GetHostName(), portNumber);

                // using (ServiceHost host = new ServiceHost(typeof(Service), new Uri("http://localhost:8000/"))) // dit is voor SOAP
                host = new WebServiceHost(typeof(Webservice), new Uri(uri)); // dit is voor REST


                // http://en.csharp-online.net/WCF_Essentials%E2%80%94Enabling_Metadata_Exchange_Programmatically
                ServiceMetadataBehavior metadataBehavior = host.Description.Behaviors.Find<ServiceMetadataBehavior>();
                if (metadataBehavior == null)
                {
                    metadataBehavior = new ServiceMetadataBehavior();
                    metadataBehavior.HttpGetEnabled = true;
                    metadataBehavior.HttpGetUrl = new Uri(uri);
                    host.Description.Behaviors.Add(metadataBehavior);
                }

                // Add our Custom Behaviour to the list of behaviours (zie http://www.thereforesystems.com/capture-xml-in-wcf-service/)
                host.Description.Behaviors.Add(new CustomBehavior());

                WebHttpBinding whb = new WebHttpBinding();
                whb.Security.Mode = WebHttpSecurityMode.TransportCredentialOnly;
                whb.Security.Transport.ClientCredentialType = HttpClientCredentialType.Basic;
                whb.Security.Transport.Realm = "FingerprintWebservice";
                // Let op volgens ms documentatie staan deze settings verschillend ingesteld,
                // maar praktijk is dat ze dezelfde waarde moeten hebben!!!
                // Is vergroot omdat POST data van torrent file niet meer verwerkt kon worden (was iets 
                // groter dan 48 kb, waardoor html encodeing het waarschijnlijk over de 64kb default 
                // limit gooide)
                whb.MaxBufferSize = 1024 * 2048; // 2mb
                whb.MaxReceivedMessageSize = 1024 * 2048; // 2mb

                ServiceEndpoint ep;
                ep = host.AddServiceEndpoint(typeof(IWebserviceContract), GetWebHttpBinding(whb), uri); // Global website
                ep.Behaviors.Add(new WebHttpBehavior());
                ep.Behaviors.Add(new GZipEncoder.ContentEncodingBehavior());
                
                ServiceCredentials serviceCred = host.Description.Behaviors.Find<ServiceCredentials>();
                if (serviceCred == null)
                {
                    serviceCred = new ServiceCredentials();
                    serviceCred.UserNameAuthentication.UserNamePasswordValidationMode = System.ServiceModel.Security.UserNamePasswordValidationMode.Custom;
                    serviceCred.UserNameAuthentication.CustomUserNamePasswordValidator = new CustomWebAuthenticationValidator();
                    host.Description.Behaviors.Add(serviceCred);
                }

                // Start de service
                host.Open();


                // --------------------------------------------------------------------------------------------------------------
                // zorg dat de health test urls zonder authentication beschikbaar zijn!
                // --------------------------------------------------------------------------------------------------------------
                hostHealthService = new WebServiceHost(typeof(ServiceHealthCheck), new Uri(uri + "engine/health/"));
                hostHealthService.Description.Behaviors.Add(new CustomBehavior());
                ServiceEndpoint ep2 = hostHealthService.AddServiceEndpoint(typeof(IServiceContractHealthCheck), new WebHttpBinding(), "");
                hostHealthService.Open();
                // --------------------------------------------------------------------------------------------------------------


                // add event handlers
                foreach (ChannelDispatcher dispatcher in host.ChannelDispatchers)
                {
                    foreach (var endPoint in dispatcher.Endpoints)
                    {
                        // get a list of MessageInspectors that are of type Inspector
                        var query = (from ex in endPoint.DispatchRuntime.MessageInspectors
                                     where ex.GetType() == typeof(Inspector)
                                     select ex).Cast<Inspector>();

                        // hook up the events
                        foreach (var item in query)
                        {
                            // Dit event heeft ook de grote van het reply vandaar dat we die gebruiken voor het loggen
                            item.RaiseSendingReply += new EventHandler<InspectorEventArgs>(LogReceivedRequest);
                        } //foreach
                    } //foreach
                } //foreach

                // Start de accouting thread (zorgt voor mailen info elke nacht)
                CreateAccountingThread();

                if (Environment.UserInteractive)
                {
                    PrintEndpoints(host);
                }

                receiveIndex = new ReceiveIndex(Path.GetDirectoryName(LuceneIndexes.SubFingerprintLocation));
            }
            catch (Exception e)
            {
                CDRLogger.Logger.LogError(e);
            }
        }

        protected override void OnStop()
        {
            CDRLogger.Logger.LogInfo("OnStop()");

            if (receiveIndex != null)
            {
                receiveIndex.Stop();
                receiveIndex = null;
            }

            CDRLogger.Logger.LogInfo("KillAccountingThread();");
            KillAccountingThread();

            Webservice.ForceServiceOffline = true;

            // stop service code goes here 
            base.OnStop();

            CDRLogger.Logger.LogInfo("serviceMutex.ReleaseMutex();");
            if (serviceMutex != null)
            {
                if (serviceMutex.WaitOne(TimeSpan.Zero, true))
                {
                    serviceMutex.ReleaseMutex();
                    serviceMutex = null;
                }
            }

            if (hostHealthService != null && hostHealthService.State == CommunicationState.Opened)
            {
                hostHealthService.Close(new TimeSpan(0, 0, 10)); // sluiten na max 10 seconden 
            }
            if (host != null && host.State == CommunicationState.Opened)
            {
                host.Close(new TimeSpan(0, 0, 10)); // sluiten na max 10 seconden 
            }

            CDRLogger.Logger.LogInfo("SMTPLogging.Close();");
            SMTPLogging.Close();
            CDRLogger.Logger.LogInfo("LogUsage.Close();");
            LogUsage.Close();
        }

        //produces a custom web service binding mapped to the obtained gzip classes
        private static Binding GetWebHttpBinding(WebHttpBinding whb)
        {
            CustomBinding customBinding = new CustomBinding(whb);
            for (int i = 0; i < customBinding.Elements.Count; i++)
            {
                if (customBinding.Elements[i] is WebMessageEncodingBindingElement)
                {
                    WebMessageEncodingBindingElement webBE = (WebMessageEncodingBindingElement)customBinding.Elements[i];
                    customBinding.Elements[i] = new GZipEncoder.GZipMessageEncodingBindingElement(webBE);
                    break;
                }
            } //for

            return customBinding;
        }
        #endregion


        #region Row Logging & and slow query detection
        static System.Globalization.CultureInfo culture = new System.Globalization.CultureInfo("en-US");
        static SlowQueryLog slowQueryLog = new SlowQueryLog();
        static bool sendSlowLog = false;
        /// <summary>
        /// Zie map ServiceLogging waar we "InspectorEventArgs" zelf maken en vullen!
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LogReceivedRequest(object sender, InspectorEventArgs e)
        {
            double timeSpentInMS = (System.DateTime.Now - e.TimeStamp).TotalMilliseconds;

            bool slowLogEntry = false;
            // =================================================================================
            // Slow query logging mail code
            // =================================================================================
            double avgTimeSpent = CollectTimeSpanSpentInMethods(Convert.ToInt32(timeSpentInMS), e.FunctionName);
            if (avgTimeSpent >= 0 && ( ((avgTimeSpent * 2) < 1000 && timeSpentInMS > 1000) || ((avgTimeSpent * 2) > 1000 && timeSpentInMS > 10000) ))
            {
                slowLogEntry = true;
                lock (slowQueryLog)
                {
                    if (slowQueryLog.NumberOfRows == 0)
                    {
                        slowQueryLog.NumberOfRows++;
                        slowQueryLog.FirstRowAdded = DateTime.Now;
                    }

                    // De functie doet er langer dan gemiddeld over! (zelf met 10% extra tijd die we hier rekenen)
                    slowQueryLog.Body.Append(e.TimeStamp.ToString("[dd/MMM/yyyy:HH:mm:ss zzz]", culture));
                    slowQueryLog.Body.Append(' ');
                    slowQueryLog.Body.Append(e.FunctionName + ": spent " + timeSpentInMS.ToString("0") + "ms while average is " + avgTimeSpent.ToString("0") + "\r\n");
                } //lock
            }

            // Detecteer of we de "slowQueryLog" moeten versturen. Doen we bij 100 berichten of na 5 minuten verzamelen
            lock (slowQueryLog)
            {
                if (slowQueryLog.NumberOfRows >= 200 || (slowQueryLog.NumberOfRows > 0 && (DateTime.Now.Date - slowQueryLog.FirstRowAdded.Date).TotalDays != 0))
                {
                    // verstuur log!
                    if (sendSlowLog)
                    {
                        SMTPLogging.AddSMTPMessage(string.Format("SlowQueryLog CDRWebService ({0})", Environment.MachineName), slowQueryLog.Body.ToString());
                    }
                    slowQueryLog.Body.Clear();
                    slowQueryLog.NumberOfRows = 0;
                }
            }
            // =================================================================================


            // Maak Apache a Like Log regel en schrijf deze weg
            StringBuilder sb = new StringBuilder(256);
            try
            {
                if (slowLogEntry)
                {
                    sb.Append("# Slow query\r\n");
                }
                sb.Append(e.TimeStamp.ToString("[dd/MMM/yyyy:HH:mm:ss zzz]", culture));
                sb.Append(' '); // seperator
                sb.Append(e.ClientIP);
                sb.Append(' '); // seperator
                sb.Append(e.Username);
                sb.Append(' '); // seperator
                sb.Append(e.Servername);
                sb.Append(' '); // seperator
                sb.Append(e.LocalIP);
                sb.Append(' '); // seperator

                // Extra info niet in normaal CDR apache log
                sb.Append(timeSpentInMS.ToString("0"));
                sb.Append("ms");
                sb.Append(' '); // seperator

                // POST of GET request info
                sb.Append('"');
                sb.Append(e.Method);
                sb.Append(' ');
                sb.Append(e.Request);
                sb.Append(" HTTP/1.0"); // faked
                sb.Append('"');
                sb.Append(' '); // seperator

                if (e.FunctionName.Length <= 0)
                {
                    // Failed dus 404
                    sb.Append("404");
                }
                else
                {
                    // function exists dus 200
                    sb.Append("200");
                }
                sb.Append(' '); // seperator

                sb.Append(e.ResponseInBytes.ToString());
                sb.Append(' '); // seperator

                sb.Append('"');
                sb.Append(e.Referer);
                sb.Append('"');
                sb.Append(' '); // seperator

                sb.Append('"');
                sb.Append(e.Cookie);
                sb.Append('"');
            }
            catch
            {
                if (e != null && e.Message != null)
                {
                    sb.Append(e.Message);
                }
            }
            LogUsage.AddLog(sb.ToString());
        }

        static System.Collections.Hashtable timeSpanMethodTable = new System.Collections.Hashtable();
        /// <summary>
        /// Returns average time spent in this function, or -1 when not average can be given
        /// 
        /// returned average is 10% more then we calculate!!
        /// </summary>
        /// <param name="timeSpentInMS"></param>
        /// <param name="functionName"></param>
        /// <returns></returns>
        private double CollectTimeSpanSpentInMethods(int timeSpentInMS, string functionName)
        {
            double avgTimeSpan = -1;

            AverageProcessingTimeItem apti = null;
            lock (timeSpanMethodTable.SyncRoot)
            {
                apti = (AverageProcessingTimeItem)timeSpanMethodTable[functionName];
            }
            if (apti == null)
            {
                // new item
                apti = new AverageProcessingTimeItem();
                apti.FunctionName = functionName;

                apti.HitCounter = 1;
                apti.TotalTimeInMS = timeSpentInMS;
                apti.TimespanList[0] = timeSpentInMS;
                apti.LastItemPtr = 0;
                apti.ItemCount = 1;                                
                apti.AvgTimeSpan = timeSpentInMS;

                lock (timeSpanMethodTable.SyncRoot)
                {
                    timeSpanMethodTable[functionName] = apti;
                }
            }
            else
            {
                lock (apti)
                {
                    // Alles onder de 200 is geen probleem
                    if (timeSpentInMS > 30000)
                    {
                        // Query duurt erg lang, niet opnemen in timespan array
                        return apti.AvgTimeSpan;
                    }

                    // Tijd toevoegen en nieuwe avg berekenen
                    apti.HitCounter++;
                    apti.LastItemPtr++;
                    if (apti.LastItemPtr >= apti.TimespanList.Length)
                    {
                        apti.LastItemPtr = 0;
                    }
                    else
                    {
                        if (apti.ItemCount < apti.TimespanList.Length)
                        {
                            apti.ItemCount++;
                        }
                    }                    

                    apti.TotalTimeInMS -= apti.TimespanList[apti.LastItemPtr];
                    apti.TotalTimeInMS += timeSpentInMS;
                    apti.TimespanList[apti.LastItemPtr] = timeSpentInMS;
                    apti.AvgTimeSpan = apti.TotalTimeInMS / apti.ItemCount;

                    if (apti.HitCounter >= 5)
                    {
                        // voeg altijd 10% extra tijd toe
                        avgTimeSpan = apti.AvgTimeSpan * 1.10;
                    }
                }
            }

            return avgTimeSpan;
        }
        #endregion


        public void ExecuteTask()
        {
            while (UserAccountManager.WritingToUserDatabase)
            {
                Thread.Sleep(50);
            }

            accountingThreadStarted = true;
            IniFile ini = null;
            try
            {
                // Om maar 1x per uur robot task te draaien
                string webServiceURL = string.Format("http://{0}:{1}/", System.Net.Dns.GetHostName(), PortNumber).ToLower();

                // Lees de statistieken die we bij afsluiten programma hebben weggeschreven
                ini = new IniFile(ServiceIniFilename);
                try
                {
                    UserAccountManager.GlobalNumberOfRequests = Convert.ToInt64(ini.IniReadValue("Statistics", "GlobalNumberOfRequests"));

                    foreach (KeyValuePair<string, UserAccount> kvp in UserAccountManager.CloneUserDatabase())
                    {
                        // voor het uitlezen hebben we genoeg aan de "clone" vane de user gegevens
                        Int64 v = Convert.ToInt64(ini.IniReadValue("Statistics", string.Format("{0}_NumberOfRequests", kvp.Key)));
                        // Nu deze gebruiker gegevens opslaan
                        UserAccount ua = UserAccountManager.Account(kvp.Key);
                        if (ua != null)
                        {
                            ua.NumberOfRequests = v;
                        }
                    } //foreach
                }
                catch
                {
                }
                ini = null;
                ClearRequestCountersToServiceIniFile();

                // Vertel dat we verzoeken kunnen ontvangen. Is voornamelijk voor haproxy!
                Webservice.ForceServiceOffline = false;

                DateTime lastCheckIndex = DateTime.MinValue;
                DateTime lastReboot = DateTime.Now.Date;
                while (accountingThreadStarted)
                {
                    try
                    {
                        if (Environment.UserInteractive)
                        {
                            // Laat op het scherm zien hoeveel request we binnen hebben.
                            int cLeft = Console.CursorLeft;
                            int cTop = Console.CursorTop;
                            try
                            {
                                Console.WriteLine(string.Format("Global number of WebService Request : {0,-18}", UserAccountManager.GlobalNumberOfRequests));
                            }
                            finally
                            {
                                Console.CursorLeft = cLeft;
                                Console.CursorTop = cTop;
                            }
                        }

                        if ((DateTime.Now - lastCheckIndex).TotalDays > 1)
                        {
                            lastCheckIndex = DateTime.Now.Date;
                            /*
                            if ((DateTime.Now - ReceiveIndex.DateTimeOfIndex).TotalDays > 9)
                            {
                                // Verstuur een mail dat de index te "oud" is
                                string message = string.Format("SubFingerindex on {0} is {1} days old", Environment.MachineName, (DateTime.Now - ReceiveIndex.DateTimeOfIndex).TotalDays);

                                Mail.SendMail(Mail.EMail_Automatisering, message, message);
                            }
                            */
                        }

                        // ==========================================================================================================
                        // Moet de computer worden herstart?
                        // ==========================================================================================================
                        if ((DateTime.Now - lastReboot).TotalDays >= 7 && (DateTime.Now.DayOfWeek == DayOfWeek.Wednesday || DateTime.Now.DayOfWeek == DayOfWeek.Thursday))
                        {
                            Console.WriteLine();
                            Console.WriteLine("Rebooting computer...");
                            Program.RestartApplication = true; // zorg dat deze service wordt gestopt!
                            // Wacht 10 seconden
                            DateTime dt = DateTime.Now;
                            while (accountingThreadStarted && Program.ApplicationState != ApplicationState.Stopped)
                            {
                                Thread.Sleep(100);
                                if ((DateTime.Now - dt).TotalMilliseconds >= (10 * 1000))
                                {
                                    // Hoe dan ook stoppen na 10 seconden
                                    break;
                                }
                            } //while

                            accountingThreadStarted = false;
                            // Wacht 4 seconden!
                            Thread.Sleep(4 * 1000);
                            Console.WriteLine("Reboot signal send.");
                            RebootWindows.Reboot();
                            break;
                        }
                        // ==========================================================================================================

                        if (accountingThreadStarted)
                        {
                            waitEvent.WaitOne(5000, false); // na 5 seconden komen we hoe dan ook even tot leven
                        }
                    }
                    catch (Exception e)
                    {
                        CDRLogger.Logger.LogError(e);
                    }
                } //while

                // Save usage statistics for next run
                WriteRequestCountersToServiceIniFile();
            }
            finally
            {
                Thread.CurrentThread.Abort();
            }

            CDRLogger.Logger.LogInfo("Ending ExecuteTask();");
        }

        #region Inifile Read/write/reset
        public static string ServiceIniFilename
        {
            get
            {
                return System.IO.Path.ChangeExtension(System.IO.Path.ChangeExtension(Assembly.GetExecutingAssembly().Location, null), ".ini");
            }
        }

        public static bool TestIniFile()
        {
            IniFile ini = new IniFile(ServiceIniFilename);
            try
            {
                ini.IniWriteValue("Statistics", "Test", "1TEST1");
                string test = ini.IniReadValue("Statistics", "Test");
                ini.IniRemoveKey("Statistics", "Test");

                return test.Equals("1TEST1");
            }
            catch { }

            return false;
        }

        private void WriteRequestCountersToServiceIniFile()
        {
            try
            {
                IniFile ini = new IniFile(ServiceIniFilename);

                ini.IniWriteValue("Statistics", "Date", DateTime.Now.ToString("yyyy-MM-dd"));
                ini.IniWriteValue("Statistics", "GlobalNumberOfRequests", UserAccountManager.GlobalNumberOfRequests.ToString());

                // Nu per gebruiker stats wegschrijven
                foreach (KeyValuePair<string, UserAccount> kvp in UserAccountManager.CloneUserDatabase())
                {
                    // voor het uitlezen hebben we genoeg aan de "clone" vane de user gegevens
                    ini.IniWriteValue("Statistics", string.Format("{0}_NumberOfRequests", kvp.Key), kvp.Value.NumberOfRequests.ToString());
                } //foreach
            }
            catch
            {
            }
        }

        private void ClearRequestCountersToServiceIniFile()
        {
            try
            {
                IniFile ini = new IniFile(ServiceIniFilename);

                ini.IniRemoveKey("Statistics", "Date"); // zet stats op gisteren met allemaal 0 waardes
                ini.IniRemoveKey("Statistics", "GlobalNumberOfRequests");

                foreach (KeyValuePair<string, UserAccount> kvp in UserAccountManager.CloneUserDatabase())
                {
                    // verwijder de "key"
                    ini.IniRemoveKey("Statistics", string.Format("{0}_NumberOfRequests", kvp.Key));
                } //foreach
            }
            catch
            {
            }
        }

        /// <summary>
        /// Unieke id voor welke webservice zodat ze zich onderling kunnen indentificeren
        /// </summary>
        public static int PortNumber
        {
            get
            {
                try
                {
                    int portNumber = 8080;
                    foreach (string cmd in Environment.GetCommandLineArgs())
                    {
                        if (cmd.Substring(0, 6).ToUpper() == "/PORT:")
                        {
                            if (int.TryParse(cmd.Substring(6), out portNumber))
                            {
                                // commandline altijd overrides ini file
                                return portNumber;
                            }
                        }
                    } //foreach

                    // nu ini file proberen
                    IniFile ini = new IniFile(ServiceIniFilename);
                    return Convert.ToInt32(ini.IniReadValue("Settings", "Port", "8080"));
                }
                catch { }

                return 8080;
            }
        }

        #endregion

        private void CreateAccountingThread()
        {
            if (accountingThread != null)
            {
                KillAccountingThread();
            }

            accountingThreadStarted = false;

            // Start de thread
            ThreadStart st = new ThreadStart(ExecuteTask); // create a thread and attach to the object
            accountingThread = new Thread(st);
            accountingThread.Start();
        }

        private void KillAccountingThread()
        {
            try
            {
                // Stop de thread's
                accountingThreadStarted = false;

                // Maak thread wakker
                waitEvent.Set(); // start de thread mocht de thread in een slaap toestand staan
                // Wacht max 1 minuut om het ding te laten stoppen
                accountingThread.Join(new TimeSpan(0, 1, 0));
                accountingThread = null;
            }
            catch { }
        }


        /// <summary>
        /// print the endpoints of current service
        /// </summary>
        /// <param name="host"></param>
        private static void PrintEndpoints(ServiceHost host)
        {
            foreach (ChannelDispatcher cd in host.ChannelDispatchers)
            {
                foreach (EndpointDispatcher ed in cd.Endpoints)
                {
                    if (ed.ContractName == "IHttpGetHelpPageAndMetadataContract")
                    {
                        Console.WriteLine("Service metadata at {0}?wsdl", ed.EndpointAddress.Uri);
                    }
                    else
                    {
                        Console.WriteLine("Service listening at {0}", ed.EndpointAddress.Uri);
                    }
                } //foreach
            }
        }

        private static string smtpServer = "smtp.example.com";
        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <returns>
        ///  0 --- Alles okay
        /// -1 --- Timeout gekregen bij versturen mail (mail na 20 seconden wachten waarschijnlijk niet verstuurd)
        /// -2 --- Onbekende fout tijdens versturen mail
        /// -3 --- Geen message
        /// </returns>
        public static int SendEmail(MailMessage message)
        {
            if (message != null)
            {
                string IniPath = CDR.DB_Helper.FingerprintIniFile;
                if (System.IO.File.Exists(IniPath))
                {
                    try
                    {
                        CDR.Ini.IniFile ini = new CDR.Ini.IniFile(IniPath);
                        smtpServer = ini.IniReadValue("Mail", "SMTPServer", smtpServer);
                    }
                    catch { }
                }

                SmtpClient client = new SmtpClient(smtpServer);
                client.Port = 25;
                client.Timeout = 30000; // 30 seconden

                // We gebruiken hier NIET de asynchrone functie. Als we dat wel zouden doen, dan volgt de exception pas nadat
                // deze method gereturned is en dan worden alle mailtjes als verstuurd beschouwd, terwijl we willen loggen als
                //      string userState = "test message1";
                try
                {
                    //client.SendAsync(message, userState);
                    client.Send(message);
                    return 0; // alles okay
                }
                catch (Exception e)
                {
                    if (e.Message == "The operation has timed out.")
                    {
                        // Blijkbaar is het mailtje al WEL verstuurd als deze foutmelding optreedt.
                        // Uit de logging moeten we kunnen halen of dit klopt.
                        // Het is nog iets gecompliceerder. De SmtpClient.timeout  stond eerst op 5 sec. 
                        // Dit was te weinig, want als de mailserver het druk had trad er vaak een timeout op.
                        // Helaas trad de timeout soms ook op als het mailtje al verstuurd was. Dus als je probeert
                        // het mailtje opnieuw te versturen na een timeout, dan werd het soms meerdere malen verstuurd.
                        // Door de timeout op 20 sec. te zetten komt deze situatie niet (zo vaak) voor. 
                        return -1;
                    }

                    // Log error
                    Console.WriteLine(e.Message);

                    return -2;
                }
            }

            return -3;
        }
    }


    public class AverageProcessingTimeItem
    {
        public string FunctionName = string.Empty;
        public int HitCounter = 0;
        public int[] TimespanList = new int[10]; // bevat laatste 10 metingen
        public long TotalTimeInMS = 0;
        public int ItemCount = 0; // how many valid items are in the list
        public int LastItemPtr = -1; // Wijst naar laatst toegevoegd item of -1 als nog niks is toegevoegd
        public double AvgTimeSpan = -1;
    }

    public class SlowQueryLog
    {
        public DateTime FirstRowAdded = DateTime.MinValue;
        public StringBuilder Body = new StringBuilder(5120);
        public int NumberOfRows = 0;
    }

    /*
    // http://www.bryancook.net/2008/04/running-multiple-net-services-within.html
    [RunInstaller(true)]
    public class MyServiceInstallerClass : Installer
    {
        public MyServiceInstallerClass()
        {
            ServiceProcessInstaller serviceProcessInstaller = new ServiceProcessInstaller();
            ServiceInstaller serviceInstaller = new ServiceInstaller();

            serviceProcessInstaller.Account = ServiceAccount.LocalSystem;

            serviceInstaller.ServiceName = Program.ServiceName;
            serviceInstaller.Description = "CDR REST Search Service voor MuziekWeb en de afgeleiden websites van de CDR";
            // serviceInstaller.ServicesDependedOn = new string[] { "Service2" };
            serviceInstaller.StartType = ServiceStartMode.Automatic;
            
            Installers.Add(serviceProcessInstaller);
            Installers.Add(serviceInstaller);
        }
    }
    */
}


/*
Install.Bat 
C:\WINDOWS\Microsoft.NET\Framework\v2.0.50727\installutil "C:\Visual Studio 2008\Projects\....\Temp.exe" 
pause 
NET START CDRRESTSearch 


Uninstall.Bat
NET STOP CDRRESTSearch 
C:\WINDOWS\Microsoft.NET\Framework\v2.0.50727\installutil /u "C:\Visual Studio 2008\Projects\....\Temp.exe" 
*/