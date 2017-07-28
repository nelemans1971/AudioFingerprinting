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
using System.IO;
using System.Linq;
using System.Text;
using System.Reflection;
using System.ServiceProcess;
using System.Xml;
using CDR;
using CDR.Logging;
using CDR.Service;
using System.Runtime.InteropServices;

namespace RadioChannelWebService
{
    class Program
    {
        public const string ServiceName = "FingerprintWebservice";

        static void Main(string[] args)
        {
            applicationState = ApplicationState.Starting;

            RenameINIFile();
            string AppName = System.IO.Path.GetFileName(System.IO.Path.ChangeExtension(Assembly.GetExecutingAssembly().Location, null));

            // http://tech.einaregilsson.com/2007/08/15/run-windows-service-as-a-console-program/
            ServiceBase[] servicesToRun = new ServiceBase[] { new WindowsService() };

            if (Environment.UserInteractive)
            {
                Version version = Assembly.GetExecutingAssembly().GetName().Version;
                string versionType = "";
#if DEBUG
                versionType = " [DEBUG]";
#endif
                string title = String.Format("{0} v{1:0}.{2:00}.{3:0000}{4}", AppName, version.Major, version.Minor, version.Build, versionType);
                Console.Title = title;
                Console.WriteLine(title);
                Console.WriteLine();

                      
                Program.RestoreConsoleWindowPosition("Settings"); // stel console positie in (als ze er zijn)
                Program.StoreConsoleWindowPosition("Settings"); // huidige positie opslaan


                foreach (string cmd in args)
                {
                    if (cmd.ToUpper() == "/INSTALL")
                    {
                        MyServiceInstaller c = new MyServiceInstaller();

                        if (c.ServiceExists(ServiceName))
                        {
                            // Als ie draait eerst stoppen
                            c.StopService(ServiceName);
                            // uninstall
                            c.UnInstallService(ServiceName);
                        }

                        // Nu installeren
                        if (c.InstallService(System.Reflection.Assembly.GetExecutingAssembly().Location, ServiceName, ServiceName, ""))
                        {
                            Console.WriteLine("Service installed and started.");
                        }
                        else
                        {
                            Console.WriteLine("Service NOT installed. We had an error.");
                        }

                        Console.WriteLine();
                        WaitForAnyKey();
                        // We zijn hoe dan ook klaar nu
                        Environment.Exit(0);
                    }
                    else if (cmd.ToUpper() == "/UNINSTALL")
                    {
                        MyServiceInstaller c = new MyServiceInstaller();

                        // Als ie draait eerst stoppen
                        c.StopService(ServiceName);

                        // Nu proberen te verwijderen
                        if (c.UnInstallService(ServiceName))
                        {
                            Console.WriteLine("Service uninstalled.");
                        }
                        else
                        {
                            Console.WriteLine("Service not found or there was an error.");
                        }

                        Console.WriteLine();
                        WaitForAnyKey();
                        // We zijn hoe dan ook klaar nu
                        Environment.Exit(0);
                    }
                } //foreach

                Console.WriteLine("Options:");
                Console.WriteLine("/INSTALL        Install this program as a windows service.");
                Console.WriteLine("/UNINSTALL      Uninstall this program from the windows service.");

                Console.WriteLine("                Nothing then running service from commandline.");
                Console.WriteLine();

                Console.WriteLine("Continuing in commandline mode...");
                Console.WriteLine();
                Console.WriteLine();

                if (WindowsService.ServiceIsRunningOnThisComputer)
                {
                    Console.WriteLine("This service is already running on this computer.");
                    Console.WriteLine("You cannot run it twice. Aborting.");
                    Environment.Exit(1);
                }

                // Startup as application 
                Console.WriteLine("Starting REST Service...");
                Console.WriteLine();

                applicationState = ApplicationState.Running;
                Type type = typeof(ServiceBase);
                BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                MethodInfo method = type.GetMethod("OnStart", flags);
                foreach (ServiceBase service in servicesToRun)
                {
                    method.Invoke(service, new object[] { args });
                }

                Console.CancelKeyPress += new ConsoleCancelEventHandler(
                    delegate(object sender, ConsoleCancelEventArgs e)
                    {
                        applicationState = ApplicationState.Stopping;
                        foreach (ServiceBase service in servicesToRun)
                        {
                            service.Stop();
                        } //foreach
                        Program.StoreConsoleWindowPosition("Settings"); // huidige positie opslaan
                        Environment.Exit(0);
                    });

                // Controleer of er geen errors zijn
                bool serviceHasError = false;
                foreach (ServiceBase service in servicesToRun)
                {
                    if ((service as WindowsService).Host.State == System.ServiceModel.CommunicationState.Faulted)
                    {
                        serviceHasError = true;
                        break;
                    }
                } //foreach

                if (!serviceHasError)
                {
                    WaitForAnyKey();
                }
                Console.WriteLine();
                applicationState = ApplicationState.Stopping;

                foreach (ServiceBase service in servicesToRun)
                {
                    service.Stop();
                } //foreach
                Program.StoreConsoleWindowPosition("Settings"); // huidige positie opslaan
            }
            else
            {
                // Startup as service.
                applicationState = ApplicationState.Running;
                ServiceBase.Run(servicesToRun);
                applicationState = ApplicationState.Stopping;
            }

            applicationState = ApplicationState.Stopped;
        }

        private static System.Threading.EventWaitHandle closeEvent = new System.Threading.EventWaitHandle(false, System.Threading.EventResetMode.AutoReset);

        private static void WaitForAnyKey()
        {
            Console.WriteLine("Press any key to exit");
            while (!Console.KeyAvailable && !restartApplication)
            {
                if (closeEvent.WaitOne(100, false)) // lijkt niet echt te werken (sleep nodig anders draait code hierna niet)
                {
                    break;
                }
            } //while

            // // Dumy de key(s)
            if (!restartApplication)
            {
                while (Console.KeyAvailable)
                {
                    Console.ReadKey(true);
                }
            }
        }

        private static bool restartApplication = false;
        /// <summary>
        /// Help var om restarten van deze consule app mogelijk te maken
        /// </summary>
        public static bool RestartApplication
        {
            get
            {
                return restartApplication;
            }
            set
            {
                restartApplication = value;         
            }
        }

        private static ApplicationState applicationState = ApplicationState.Starting;
        public static ApplicationState ApplicationState
        {
            get
            {
                return applicationState;
            }
        }


        /// <summary>
        /// Use the correct INI file, used for development!
        /// </summary>
        private static void RenameINIFile()
        {
            string iniFilename = Path.ChangeExtension(System.Reflection.Assembly.GetExecutingAssembly().Location, ".{0}");
#if DEBUG            
            string releaseFilename = string.Format(iniFilename, "DEBUG.ini");
#else
            string releaseFilename = string.Format(iniFilename, "RELEASE.ini");
#endif
            iniFilename = string.Format(iniFilename, "ini");

            if (File.Exists(releaseFilename))
            {
                try
                {
                    File.Delete(iniFilename);
                }
                catch
                {
                }
                GC.WaitForPendingFinalizers();
                File.Move(releaseFilename, iniFilename);
                GC.WaitForPendingFinalizers();
            }
        }

        #region Specific Windows Console position functions

        const int SWP_NOSIZE = 0x0001;

        [DllImport("kernel32.dll", ExactSpelling = true)]
        private static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll", EntryPoint = "SetWindowPos")]
        public static extern IntPtr SetWindowPos(IntPtr hWnd, int hWndInsertAfter, int x, int Y, int cx, int cy, int wFlags);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;        // x position of upper-left corner
            public int Top;         // y position of upper-left corner
            public int Right;       // x position of lower-right corner
            public int Bottom;      // y position of lower-right corner
        }

        private static IntPtr myConsole = GetConsoleWindow();


        public static void RestoreConsoleWindowPosition(string section)
        {
            try
            {
                string iniFilename = Path.ChangeExtension(System.Reflection.Assembly.GetExecutingAssembly().Location, ".ini");

                if (File.Exists(iniFilename))
                {
                    CDR.Ini.IniFile ini = new CDR.Ini.IniFile(iniFilename);
                    int xPos = Convert.ToInt32(ini.IniReadValue(section, "WindowXPos", "0"));
                    int yPos = Convert.ToInt32(ini.IniReadValue(section, "WindowYPos", "0"));
                    if (xPos != 0 && yPos != 0)
                    {
                        SetWindowPos(myConsole, 0, xPos, yPos, 0, 0, SWP_NOSIZE);
                    }
                }
            }
            catch
            {
            }
        }

        public static void StoreConsoleWindowPosition(string section)
        {
            try
            {
                string iniFilename = Path.ChangeExtension(System.Reflection.Assembly.GetExecutingAssembly().Location, ".ini");

                RECT rect;
                if (GetWindowRect(myConsole, out rect))
                {
                    CDR.Ini.IniFile ini = new CDR.Ini.IniFile(iniFilename);
                    ini.IniWriteValue(section, "WindowXPos", rect.Left.ToString());
                    ini.IniWriteValue(section, "WindowYPos", rect.Top.ToString());
                }
            }
            catch
            {
            }
        }

        #endregion
    }

    public enum ApplicationState
    {
        Starting = 0,
        Running,
        Stopping,
        Stopped
    }
}
