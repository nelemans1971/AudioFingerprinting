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
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;


namespace MatchAudio
{
    class Program
    {
        static void Main(string[] args)
        {
            string appName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
            Version version = Assembly.GetExecutingAssembly().GetName().Version;
            string debug = "";
#if DEBUG
            debug = " [DEBUG]";
#endif
            string appVersion = String.Format("{0}{1} v{2:0}.{3:00}", appName, debug, version.Major, version.Minor);
            Console.Title = appVersion;
            Console.WriteLine(appVersion);
            Console.WriteLine();

            if (IntPtr.Size == 4)
            {
                Console.WriteLine("This application must run in 64bits mode.");
                Environment.Exit(1);
            }

            Worker worker = new Worker();
            worker.RunAcoustIDTest();
            worker.RunSubFingerTest();

#if DEBUG
            Console.WriteLine("Press enter to close console.");
            Console.ReadLine();
#endif
        }
    }
}
