using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CDR;
using MySql.Data.MySqlClient;

namespace CreateDatabase
{
    class Worker
    {
        public void Run()
        {
            CDR.DB_Helper.Initialize();

            Console.WriteLine("Making connection to MySQL DB.");
            Console.WriteLine(string.Format("Server   : {0}", DB_Helper.mysqlServer));
            Console.WriteLine(string.Format("Port     : {0}", DB_Helper.mysqlPort));
            Console.WriteLine(string.Format("Database : {0}", DB_Helper.mysqlDB));
            Console.WriteLine(string.Format("Username : {0}", DB_Helper.mysqlUser));
            Console.WriteLine(string.Format("Password : {0}", DB_Helper.mysqlPassword));
            Console.WriteLine();
            Console.WriteLine("Correct settings in 'Fingerprint.ini' file under [MySQL] if incorrect.");
            Console.WriteLine();
            Console.WriteLine();

            using (MySqlConnection conn = CDR.DB_Helper.NewMySQLConnection())
            {
                if (conn == null)
                {
                    Console.WriteLine("No connection with database could be made.");
                    return;
                }
            }

            string fCreateDatase = Path.GetFullPath(@"..\..\..\DatabaseScripts\1.CreateDatabase.sql");
            if (!File.Exists(fCreateDatase))
            {
                Console.WriteLine(string.Format("Can't find Create database script : {0}", Path.GetFileName(fCreateDatase)));
                return;
            }
            Console.WriteLine(string.Format("Warning server {0}, database {1} will be destroyed!", DB_Helper.mysqlServer, DB_Helper.mysqlDB));
            Console.Write("Continue (Y/N)? ");
            if (AskYesNo())
            {
                Console.WriteLine("Intializing MySQL database for fingerprint software.");
                if (!RestoreFromScript(fCreateDatase))
                {
                    return;
                }
                Console.WriteLine();
            }

            // or use mysql itself to restore the data (THis is A LOT FASTER!):
            // 
            // "C:\Program Files\MySQL\MySQL Server 5.7\bin\mysql" -uroot -p fingerprint < titelnummertrack_id.sql
            // "C:\Program Files\MySQL\MySQL Server 5.7\bin\mysql" -uroot -p fingerprint < fingerid.sql
            // "C:\Program Files\MySQL\MySQL Server 5.7\bin\mysql" -uroot -p fingerprint < subfingerid.sql


            string fTable1 = Path.GetFullPath(@"titelnummertrack_id.sql");
            string fTable2 = Path.GetFullPath(@"fingerid.sql");
            string fTable3 = Path.GetFullPath(@"subfingerid.sql");

            if (!(File.Exists(fTable1) || File.Exists(fTable2) || File.Exists(fTable3)))
            {
                return;
            }
            Console.Write("Restore tables from sqldump (Y/N)? '");
            if (!AskYesNo())
            {
                return;
            }


            if (File.Exists(fTable1))
            {
                DateTime dtStart = DateTime.Now;
                Console.WriteLine("Restoring table 'titelnummertrack_id' from backup file.");
                if (!RestoreFromMySQLDump(fTable1))
                {
                    return;
                }
                Console.WriteLine("Restoretime: " + (DateTime.Now - dtStart).ToString("G"));
                Console.WriteLine();
            }
            if (File.Exists(fTable2))
            {
                DateTime dtStart = DateTime.Now;
                Console.WriteLine("Restoring table 'fingerid' from backup file.");
                if (!RestoreFromMySQLDump(fTable2))
                {
                    return;
                }
                Console.WriteLine("Restoretime: " + (DateTime.Now - dtStart).ToString("G"));
                Console.WriteLine();
            }
            if (File.Exists(fTable3))
            {
                DateTime dtStart = DateTime.Now;
                Console.WriteLine("Restoring table 'subfingerid' from backup file.");
                if (!RestoreFromMySQLDump(fTable3))
                {
                    return;
                }
                Console.WriteLine("Restoretime: " + (DateTime.Now - dtStart).ToString("G"));
                Console.WriteLine();
            }
        }

        private bool AskYesNo()
        {
            while (true)
            {
                ConsoleKeyInfo key = Console.ReadKey(true);
                if (char.ToUpper(key.KeyChar) == 'Y')
                {
                    Console.WriteLine("Y");
                    return true;
                }
                else if (char.ToUpper(key.KeyChar) == 'N')
                {
                    Console.WriteLine("N");
                    return false;
                }
            }
        }

        /// <summary>
        /// Simple MySQLDump parser. Not all cases are supported.
        /// </summary>
        private bool RestoreFromMySQLDump(string filename)
        {
            if (File.Exists(filename))
            {
                long filesize = new System.IO.FileInfo(filename).Length;
                int countSkipStep = 1;
                // guess number of lines based on filesize
                if ((filesize / 80) > 10000)
                {
                    //countSkipStep = 1000;
                }

                using (MySqlConnection conn = CDR.DB_Helper.NewMySQLConnection())
                {
                    using (StreamReader sr = new StreamReader(filename))
                    {
                        StringBuilder sb = new StringBuilder();
                        String line;
                        long count = 0;
                        try
                        {
                            while ((line = sr.ReadLine()) != null)
                            {
                                // ignore comments
                                if (line.Length >= 2 && line.Substring(0, 2) == "--")
                                {
                                    continue;
                                }
                                else if (line.Length >= 2 && (line.Substring(0, 2) == "//" || line.Substring(0, 2) == "/*" || line.Substring(0, 2) == "*/"))
                                {
                                    continue;
                                }
                                else if (line.Length > 6 && line.Substring(0, 3) == "/*!" && line.Substring(line.Length - 4, 3) == "*/;")
                                {
                                    // Special command probally
                                    line = line.Replace("/*!", "").Replace("*/;", "");
                                    int p = line.IndexOf(' ');
                                    if (p > 0)
                                    {
                                        line = line.Substring(p).Trim();
                                        if (!Exec_MySQL_Text(conn, line))
                                        {
                                            Console.WriteLine();
                                            Console.WriteLine("Error while restoring. Script stopped!");
                                            return false;
                                        }
                                    }
                                }
                                else if (line.Length > 0)
                                {
                                    if (line.Substring(0, 12) == "INSERT INTO ")
                                    {
                                        if (sb.Length > 0)
                                        {
                                            Exec_MySQL_Text(conn, sb);
                                            sb.Clear();
                                        }

                                        Exec_MySQL_Text(conn, line);
                                    }
                                    else
                                    {
                                        sb.Append(line);
                                    }
                                }
                                else if (line.Length > 0 && sb.Length > 0)
                                {
                                    Exec_MySQL_Text(conn, sb);
                                    sb.Clear();
                                }

                                count++;
                                if (count % countSkipStep == 0)
                                {
                                    Console.Write(string.Format("\rLine #{0:0000000000}", count));
                                }
                            }
                        }
                        finally
                        {
                            Console.WriteLine(string.Format("\rLine #{0:0000000000}", count));
                        }
                    } // using Streamreader
                } //using Conn

                return true;
            }

            return false;
        }

        /// <summary>
        /// Reads entire script and dumps it to MySQL
        /// </summary>
        private bool RestoreFromScript(string filename)
        {
            if (File.Exists(filename))
            {
                using (MySqlConnection conn = CDR.DB_Helper.NewMySQLConnection())
                {
                    StringBuilder sb = new StringBuilder();
                    using (StreamReader sr = new StreamReader(filename))
                    {
                        String line;
                        long count = 0;
                        try
                        {
                            while ((line = sr.ReadLine()) != null)
                            {
                                sb.Append(line);
                                count++;
                                Console.Write(string.Format("\rLine #{0:0000000000}", count));
                            } //while
                        }
                        finally
                        {
                            Console.WriteLine(string.Format("\rLine #{0:0000000000}", count));
                        }
                    } // using Streamreader

                    Exec_MySQL_Text(conn, sb);
                } //using Conn

                return true;
            }

            return false;
        }

        public static bool Exec_MySQL_Text(MySqlConnection conn, string sqlCmd)
        {
            try
            {
                MySqlCommand command = new MySqlCommand();
                command.Connection = conn;
                command.CommandType = CommandType.Text;
                command.CommandText = sqlCmd;
                command.CommandTimeout = 0; // unlimited

                command.ExecuteNonQuery();

                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine("Database error");
                Console.WriteLine(e.ToString());
            }

            return false;
        }

        public static bool Exec_MySQL_Text(MySqlConnection conn, StringBuilder sb)
        {
            try
            {
                MySqlCommand command = new MySqlCommand();
                command.Connection = conn;
                command.CommandType = CommandType.Text;
                command.CommandText = sb.ToString();
                command.CommandTimeout = 0; // unlimited

                command.ExecuteNonQuery();

                return true;
            }
            catch(Exception e)
            {
                Console.WriteLine("Database error");
                Console.WriteLine(e.ToString());
            }

            return false;
        }
    }
}
