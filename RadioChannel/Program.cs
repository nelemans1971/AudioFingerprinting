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
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;


namespace RadioChannel
{
    class Program
    {
        static void Main(string[] args)
        {
            Version version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            string versionType = "";
#if DEBUG
            versionType = " [DEBUG]";
#endif
            string title = String.Format("RadioChannel v{0:0}.{1:00}{2}", version.Major, version.Minor, versionType);
            Console.Title = title;
            Console.WriteLine(title);
            Console.WriteLine();

            if (IntPtr.Size == 4)
            {
                Console.WriteLine("This application must run in 64bits mode.");
                Environment.Exit(1);
            }

            string iniFilename = Path.ChangeExtension(System.Reflection.Assembly.GetExecutingAssembly().Location, ".ini");

            string radioCode = string.Empty;
            if (File.Exists(iniFilename))
            {
                CDR.Ini.IniFile ini = new CDR.Ini.IniFile(iniFilename);
                radioCode = ini.IniReadValue("Settings", "RadioCode", "");
            }
            if (args.Length > 0)
            {
                foreach (string arg in args)
                {
                    if (arg.Length > 7 && arg.ToUpper().Substring(0, 7) == "/RADIO:")
                    {
                        radioCode = arg.ToUpper().Substring(7);
                    }
                }
            }

            if (File.Exists(iniFilename))
            {
                CDR.Ini.IniFile ini = new CDR.Ini.IniFile(iniFilename);
                int xPos = Convert.ToInt32(ini.IniReadValue(radioCode, "WindowXPos", "0"));
                int yPos = Convert.ToInt32(ini.IniReadValue(radioCode, "WindowYPos", "0"));
                if (xPos != 0 && yPos != 0)
                {
                    SetWindowPos(myConsole, 0, xPos, yPos, 0, 0, SWP_NOSIZE);
                }
            }
            Program.StoreConsoleWindowPosition(radioCode);

            Channel channel = new Channel();
            channel.RunChannel(radioCode);

            Program.StoreConsoleWindowPosition(radioCode);
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


        public static void StoreConsoleWindowPosition(string radioCode)
        {
            string iniFilename = Path.ChangeExtension(System.Reflection.Assembly.GetExecutingAssembly().Location, ".ini");

            RECT rect;
            if (GetWindowRect(myConsole, out rect))
            {
                CDR.Ini.IniFile ini = new CDR.Ini.IniFile(iniFilename);
                ini.IniWriteValue(radioCode, "WindowXPos", rect.Left.ToString());
                ini.IniWriteValue(radioCode, "WindowYPos", rect.Top.ToString());
            }
        }

        #endregion
    }
}
