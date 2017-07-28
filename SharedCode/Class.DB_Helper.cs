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
using System.Text;
using System.Data;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.Reflection;
using System.IO;
using MySql.Data.MySqlClient;

namespace CDR
{
    public static class DB_Helper
    {
        private const string MYSQLCONNECTION_STRING = "Connection Protocol=Sockets;Connect Timeout=30;Server={0};Database={1};User Id={2};Password={3};Port={4}; Pooling=false;Maximum Pool Size=100;Minimum Pool Size=0;Connection Reset=True;AllowUserVariables=true;Keepalive=0";

        public static string mysqlServer = "localhost";
        public static string mysqlPort = "3306";
        public static string mysqlDB = "fingerprint";
        public static string mysqlUser = "root";
        public static string mysqlPassword = "123456";

        private static bool initialized = false;


        public static void Initialize()
        {
            if (!initialized)
            {
                string IniPath = CDR.DB_Helper.FingerprintIniFile;
                if (System.IO.File.Exists(IniPath))
                {
                    try
                    {
                        CDR.Ini.IniFile ini = new CDR.Ini.IniFile(IniPath);
                        mysqlServer = ini.IniReadValue("MySQL", "mysqlServer", mysqlServer);
                        mysqlPort = ini.IniReadValue("MySQL", "mysqlPort", mysqlPort);
                        mysqlDB = ini.IniReadValue("MySQL", "mysqlDB", mysqlDB);
                        mysqlUser = ini.IniReadValue("MySQL", "mysqlUser", mysqlUser);
                        mysqlPassword = ini.IniReadValue("MySQL", "mysqlPassword", mysqlPassword);
                    }
                    catch { }
                }
                initialized = true;
            }
        }

        public static MySqlConnection NewMySQLConnection()
        {
            Initialize();
            MySqlConnection connection = null;

            for (int i = 1; i <= 3; i++)
            {
                connection = new MySqlConnection();

                try
                {
                    string connectionString = string.Format(MYSQLCONNECTION_STRING, mysqlServer, mysqlDB, mysqlUser, mysqlPassword, mysqlPort);

                    connection.ConnectionString = connectionString;
                    connection.Open();


                    MySqlCommand command = new MySqlCommand();
                    command.Connection = connection;
                    command.CommandTimeout = 2 * 60;

                    // We willen natuurlijk in utf8 praten
                    command.CommandText = "SET NAMES utf8;";
                    command.ExecuteNonQuery();

                    // wacht 8 uur maximaal voordat na geen gebruik de connectie wordt beeindigd
                    command.CommandText = "SET SESSION wait_timeout=28800;";
                    command.ExecuteNonQuery();
                }
                catch (Exception e)
                {
                    connection = null;
                    if (i >= 3)
                    {
                        System.Threading.Thread.Sleep(500);
                        Console.WriteLine(e.ToString());
                        // Log the error 
                    }
                }
            } //for i

            return connection;
        }


        public static string FingerprintIniFile
        {
            get
            {
                long fSize = -1;
                string IniPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), @"..\..\..\AudioFingerprint\Fingerprint.Override.ini"));
                if (System.IO.File.Exists(IniPath))
                {
                    fSize = new System.IO.FileInfo(IniPath).Length;
                }
                // this is used to detect a dummy override ini file
                if (fSize < 100)
                {
                    IniPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "Fingerprint.ini");
                }

                return IniPath;
            }
        }

    }
}
