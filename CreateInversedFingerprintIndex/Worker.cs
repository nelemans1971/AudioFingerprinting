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
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AcoustID;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Store;
using MySql.Data.MySqlClient;
using System.IO;
using System.Reflection;
using AudioFingerprint.Audio;

namespace CreateInversedFingerprintIndex
{
    class Worker
    {
        public void Run()
        {
            string IniPath = CDR.DB_Helper.FingerprintIniFile;
            CDR.Ini.IniFile ini = new CDR.Ini.IniFile(IniPath);

            string path = System.Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string luceneIndexPath = ini.IniReadValue("Program", "LuceneIndexPath", Path.Combine(path, @"DB")); // Mag geen Drive letter bevatten!
            if (!luceneIndexPath.Contains(":") || (luceneIndexPath.Length > 0 && luceneIndexPath[0] != '\\'))
            {
                luceneIndexPath = Path.Combine(path, luceneIndexPath);
            }
            string acoustIDFingerMap = ini.IniReadValue("Program", "AcoustIDFingerMap", "AcoustIDFingerMap");
            string subFingerMap = ini.IniReadValue("Program", "SubFingerMap", "SubFingerLookup");

            AudioFingerprint.Audio.BassLifetimeManager.bass_EMail = ini.IniReadValue("BASS", "bass_EMail", "");
            AudioFingerprint.Audio.BassLifetimeManager.bass_RegistrationKey = ini.IniReadValue("BASS", "bass_RegistrationKey", "");


            AudioFingerprint.Math.SimilarityUtility.InitSimilarityUtility();

            CreateAcoustIDFingerLookupIndex(Path.Combine(luceneIndexPath, acoustIDFingerMap));
            CreateSubFingerLookupIndex(Path.Combine(luceneIndexPath, subFingerMap));
        }

        #region Create Finger Lucene Database (Based on AcoustID/Chromekey, https://musicbrainz.org/doc/AcoustID)

        public bool CreateAcoustIDFingerLookupIndex(string luceneIndexPath)
        {
            DateTime startTime = DateTime.Now;
            DateTime endTime = startTime;
            Console.WriteLine("Creating acoustID Finger index");

            int minID;
            int maxID;
            if (!Exec_MySQL_MinAndMax_IDS(out minID, out maxID) && minID >= 1)
            {
                return false;
            }

            if (!System.IO.Directory.Exists(luceneIndexPath))
            {
                System.IO.Directory.CreateDirectory(luceneIndexPath);
            }
            ClearFolder(luceneIndexPath);


            int fingerCount = 0;
            Lucene.Net.Store.Directory directory = FSDirectory.Open(new System.IO.DirectoryInfo(luceneIndexPath));
            IndexWriter iw = null;
            try
            {
                iw = new IndexWriter(directory, new StandardAnalyzer(Lucene.Net.Util.Version.LUCENE_30), true, IndexWriter.MaxFieldLength.UNLIMITED);
                iw.UseCompoundFile = false;
                iw.SetSimilarity(new CDR.Indexer.DefaultSimilarityExtended());
                iw.MergeFactor = 10; // default = 10
                iw.SetRAMBufferSizeMB(512 * 3);                                   // use memory to do a flush
                iw.SetMaxBufferedDocs(IndexWriter.DISABLE_AUTO_FLUSH);            // only use memory as trigger to do a flush
                iw.SetMaxBufferedDeleteTerms(IndexWriter.DISABLE_AUTO_FLUSH);     // only use memory as trigger to do a flush

                Document doc = new Document();
                doc.Add(new Field("TITELNUMMERTRACK_ID", "", Field.Store.YES, Field.Index.NOT_ANALYZED));
                doc.Add(new Field("FINGERID", "", Field.Store.NO, Field.Index.ANALYZED));

                Field fTitelnummertrackID = doc.GetField("TITELNUMMERTRACK_ID");
                fTitelnummertrackID.OmitNorms = true;
                fTitelnummertrackID.OmitTermFreqAndPositions = true;

                Field fFingerID = doc.GetField("FINGERID");
                fFingerID.OmitNorms = true;
                fFingerID.OmitTermFreqAndPositions = true;

                StringBuilder sb = new StringBuilder(256 * 1024);
                int start = minID;
                int count = 5000;
                while (start <= maxID)
                {
                    DataTable dt;
                    if (Exec_MySQL_LOADFINGERIDS(start, (start + count - 1), out dt))
                    {
                        foreach (DataRow row in dt.Rows)
                        {
                            fingerCount++;
                            if ((fingerCount % 100) == 0 || fingerCount <= 1)
                            {
                                Console.Write("\rIndexing fingerprint #" + fingerCount.ToString());
                            }

                            FingerprintAcoustID fingerprint = new FingerprintAcoustID();
                            fingerprint.Signature = (byte[])row["SIGNATURE"];

                            // Bij AcoustiID halen we vanaf positie 80 (int32 gerekend) 120 (360 nu) int lang eruit. We bepalen de 28 belangrijkste bits
                            // en slaan deze getallen vervolgens op.
                            // Dit kan omdat we een fingerprint hebben van de gehele song (eigenlijk eerste 120 seconden) en die ook gaan vergelijken met
                            // een fingerprint die aan dezelfde voorwaarde voldoet.
                            sb.Clear();
                            List<int> query = new List<int>();
                            foreach (int subFingerValue in fingerprint.AcoustID_Extract_Query)
                            {
                                // deduplicate
                                if (query.Contains(subFingerValue))
                                {
                                    continue;
                                }
                                query.Add(subFingerValue);
                                sb.Append(subFingerValue.ToString());
                                sb.Append(' ');
                            }

                            fTitelnummertrackID.SetValue(row["TITELNUMMERTRACK_ID"].ToString());
                            fFingerID.SetValue(sb.ToString());

                            iw.AddDocument(doc);
                        } //foreach
                        Console.Write("\rIndexing fingerprint #" + fingerCount.ToString());
                        start += count;
                    }
                    else
                    {
                        if (!RetryDatabaseError())
                        {
                            return false;
                        }
                    }
                } //while
            }
            finally
            {
                Console.WriteLine();
                Console.WriteLine("Optimizing.");
                if (iw != null)
                {
                    // Optimaliseer de index nu
                    iw.Commit();
                    iw.Optimize(1, true);
                    iw.Dispose();
                    iw = null;
                    GC.WaitForPendingFinalizers();
                }
            }

            endTime = DateTime.Now;
            TimeSpan ts = (endTime - startTime);
            Console.WriteLine(String.Format("Elapsed index time {0:00}:{1:00}:{2:00}.{3:000}", ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds));
            Console.WriteLine();

            return true;
        }

        #endregion

        #region Create SubFinger Lucene Database functions

        public bool CreateSubFingerLookupIndex(string luceneIndexPath)
        {
            DateTime startTime = DateTime.Now;
            DateTime endTime = startTime;
            Console.WriteLine("Creating SubFingerLookup index.");

            int minID;
            int maxID;
            if (!Exec_MySQL_MinAndMax_IDS(out minID, out maxID) && minID >= 1)
            {
                return false;
            }

            if (!System.IO.Directory.Exists(luceneIndexPath))
            {
                System.IO.Directory.CreateDirectory(luceneIndexPath);
            }
            ClearFolder(luceneIndexPath);


            Lucene.Net.Store.Directory directory = FSDirectory.Open(new System.IO.DirectoryInfo(luceneIndexPath));
            IndexWriter iw = null;
            int fingerCount = 0;
            try
            {
                iw = new IndexWriter(directory, new StandardAnalyzer(Lucene.Net.Util.Version.LUCENE_30), true, IndexWriter.MaxFieldLength.UNLIMITED);
                iw.UseCompoundFile = false;
                iw.SetSimilarity(new CDR.Indexer.DefaultSimilarityExtended());
                iw.MergeFactor = 10; // default = 10
                iw.SetRAMBufferSizeMB(512 * 3);                                   // use memory to do a flush
                iw.SetMaxBufferedDocs(IndexWriter.DISABLE_AUTO_FLUSH);            // only use memory as trigger to do a flush
                iw.SetMaxBufferedDeleteTerms(IndexWriter.DISABLE_AUTO_FLUSH);     // only use memory as trigger to do a flush

                Document doc = new Document();
                doc.Add(new Field("FINGERID", "", Field.Store.YES, Field.Index.NOT_ANALYZED));
                doc.Add(new Field("SUBFINGER", "", Field.Store.NO, Field.Index.ANALYZED));

                Field fFingerID = doc.GetField("FINGERID");
                fFingerID.OmitNorms = true;
                fFingerID.OmitTermFreqAndPositions = true;

                Field fSubFinger = doc.GetField("SUBFINGER");
                fSubFinger.OmitNorms = true;
                fSubFinger.OmitTermFreqAndPositions = true;

                StringBuilder sb = new StringBuilder(256 * 1024);

                int start = minID;
                int count = 5000;
                while (start <= maxID)
                {
                    DataTable dt;
                    if (Exec_MySQL_LOADSUBFINGERIDS(start, (start + count - 1), out dt))
                    {
                        foreach (DataRow row in dt.Rows)
                        {
                            fingerCount++;
                            if ((fingerCount % 100) == 0 || fingerCount <= 1)
                            {
                                Console.Write("\rIndexing subfingerprint #" + fingerCount.ToString());
                            }

                            FingerprintSignature fingerprint = new FingerprintSignature((string)row["TITELNUMMERTRACK"], Convert.ToInt64(row["TITELNUMMERTRACK_ID"]), (byte[])row["SIGNATURE"], Convert.ToInt64(row["DURATIONINMS"]));

                            sb.Clear();
                            for (int i = 0; i < fingerprint.SubFingerprintCount; i++)
                            {
                                uint subFingerValue = fingerprint.SubFingerprint(i);
                                int bits = AudioFingerprint.Math.SimilarityUtility.HammingDistance(subFingerValue, 0);
                                if (bits < 10 || bits > 22) // 5 27
                                {
                                    continue;
                                }
                                sb.Append(subFingerValue.ToString());
                                sb.Append(' ');
                            }
                            fFingerID.SetValue(row["TITELNUMMERTRACK_ID"].ToString());
                            fSubFinger.SetValue(sb.ToString());

                            iw.AddDocument(doc);
                        } //foreach
                        Console.Write("\rIndexing subfingerprint #" + fingerCount.ToString());

                        start += count;
                    } //if
                    else
                    {
                        if (!RetryDatabaseError())
                        {
                            return false;
                        }
                    }
                } // while alle fingerprints
            }
            finally
            {
                Console.WriteLine();
                Console.WriteLine("Optimizing.");
                if (iw != null)
                {
                    // Optimaliseer de index nu
                    iw.Commit();
                    iw.Optimize(1, true);
                    iw.Dispose();
                    iw = null;
                    GC.WaitForPendingFinalizers();
                }
            }


            endTime = DateTime.Now;
            TimeSpan ts = (endTime - startTime);
            Console.WriteLine(String.Format("Elapsed index time {0:00}:{1:00}:{2:00}.{3:000}", ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds));
            Console.WriteLine();

            return true;
        }


        /// <summary>
        /// Create SubFinger fingerprint database where all fingerprint are stored. (just like the database)
        /// </summary>
        public bool CreateSubFingerDatabase(string luceneDBPath)
        {
            DateTime startTime = DateTime.Now;
            DateTime endTime = startTime;
            Console.WriteLine("Creating SubFingerprint Database.");

            int minID;
            int maxID;
            if (!Exec_MySQL_MinAndMax_IDS(out minID, out maxID) && minID >= 1)
            {
                return false;
            }

            if (!System.IO.Directory.Exists(luceneDBPath))
            {
                System.IO.Directory.CreateDirectory(luceneDBPath);
            }
            ClearFolder(luceneDBPath);


            Lucene.Net.Store.Directory directory = FSDirectory.Open(new System.IO.DirectoryInfo(luceneDBPath));
            IndexWriter iw = null;
            try
            {
                PerFieldAnalyzerWrapper analyzerWrapper = new PerFieldAnalyzerWrapper(new StandardAnalyzer(Lucene.Net.Util.Version.LUCENE_30));
                analyzerWrapper.AddAnalyzer("FINGERID", new KeywordAnalyzer());
                analyzerWrapper.AddAnalyzer("TITELNUMMERTRACK", new KeywordAnalyzer());
                analyzerWrapper.AddAnalyzer("DURATIONINMS", new KeywordAnalyzer());
                analyzerWrapper.AddAnalyzer("UNIFORMETITELLINK", new KeywordAnalyzer());
                analyzerWrapper.AddAnalyzer("AUDIOSOURCE", new KeywordAnalyzer());
                analyzerWrapper.AddAnalyzer("FINGERPRINT", new KeywordAnalyzer());

                iw = new ThreadedIndexWriter(directory, analyzerWrapper, true, IndexWriter.MaxFieldLength.UNLIMITED);
                iw.UseCompoundFile = false;
                iw.SetSimilarity(new CDR.Indexer.DefaultSimilarityExtended());
                iw.MergeFactor = 10000; // default = 10
                iw.SetRAMBufferSizeMB(2000);                                      // use memory to do a flush
                iw.SetMaxBufferedDocs(IndexWriter.DISABLE_AUTO_FLUSH);            // only use memory as trigger to do a flush
                iw.SetMaxBufferedDeleteTerms(IndexWriter.DISABLE_AUTO_FLUSH);     // only use memory as trigger to do a flush



                int fingerCount = 0;
                int start = minID;
                int count = 5000;
                while (start <= maxID)
                {
                    DataTable dt;
                    if (Exec_MySQL_LOADSUBFINGERIDS(start, (start + count - 1), out dt))
                    {
                        foreach (DataRow row in dt.Rows)
                        {
                            fingerCount++;
                            if ((fingerCount % 100) == 0 || fingerCount <= 1)
                            {
                                Console.Write("\rIndexing subfingerprint #" + fingerCount.ToString());
                            }

                            Document doc = new Document();
                            doc.Add(new Field("FINGERID", row["TITELNUMMERTRACK_ID"].ToString(), Field.Store.YES, Field.Index.ANALYZED));
                            doc.Add(new Field("TITELNUMMERTRACK", row["TITELNUMMERTRACK"].ToString(), Field.Store.YES, Field.Index.ANALYZED));
                            doc.Add(new Field("DURATIONINMS", row["DURATIONINMS"].ToString(), Field.Store.YES, Field.Index.NO));
                            doc.Add(new Field("UNIFORMETITELLINK", row["UNIFORMETITELLINK"].ToString(), Field.Store.YES, Field.Index.NO));
                            doc.Add(new Field("AUDIOSOURCE", row["AUDIOSOURCE"].ToString(), Field.Store.YES, Field.Index.NO));
                            doc.Add(new Field("FINGERPRINT", (byte[])row["SIGNATURE"], Field.Store.YES));

                            iw.AddDocument(doc);
                        } //foreach
                        Console.Write("\rIndexing subfingerprint #" + fingerCount.ToString());
                        start += count;
                    } //if
                    else
                    {
                        if (!RetryDatabaseError())
                        {
                            return false;
                        }
                    }
                } // while alle fingerprints
            }
            finally
            {
                Console.WriteLine();
                Console.WriteLine("Optimizing.");
                if (iw != null)
                {
                    // Optimaliseer de index nu
                    iw.Commit();
                    iw.Optimize(1, true);
                    iw.Dispose();
                    iw = null;
                    GC.WaitForPendingFinalizers();
                }
            }

            endTime = DateTime.Now;
            TimeSpan ts = (endTime - startTime);
            Console.WriteLine(String.Format("Elapsed index time {0:00}:{1:00}:{2:00}.{3:000}", ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds));
            Console.WriteLine();

            return true;
        }

        #endregion

        private bool RetryDatabaseError()
        {
            Console.WriteLine();
            Console.Write("Database error: Retry (Y/N)? ");
            ConsoleKeyInfo key = Console.ReadKey(true);
            if (char.ToUpper(key.KeyChar) == 'Y')
            {
                Console.WriteLine("Y");
                return true;
            }

            Console.WriteLine("N");
            Console.WriteLine("Stopping.");
            return false;
        }

        private void ClearFolder(string folderName)
        {
            DirectoryInfo dir = new DirectoryInfo(folderName);

            foreach (FileInfo fi in dir.GetFiles())
            {
                fi.IsReadOnly = false;
                fi.Delete();
            } //foreach

            foreach (DirectoryInfo di in dir.GetDirectories())
            {
                ClearFolder(di.FullName);
                di.Delete();
            } //foreach
        }

        #region MySQL

        public static bool Exec_MySQL_MinAndMax_IDS(out int minID, out int maxID)
        {
            minID = -1;
            maxID = -1;
            // nu zorgen dat computer naam in de database komt en wij deze als ID
            // in deze class opslaan
            try
            {
                using (MySqlConnection conn = CDR.DB_Helper.NewMySQLConnection())
                {
                    MySqlCommand command = new MySqlCommand();
                    command.CommandType = CommandType.Text;
                    command.CommandText = "SELECT MIN(TITELNUMMERTRACK_ID) AS MIN_TITELNUMMERTRACK_ID,\r\n" +
                                          "       MAX(TITELNUMMERTRACK_ID) AS MAX_TITELNUMMERTRACK_ID\r\n" +
                                          "FROM   TITELNUMMERTRACK_ID\r\n";
                    command.Connection = conn;
                    command.CommandTimeout = 10 * 60; // max 10 minuten voordat we een timeout genereren

                    MySqlDataAdapter adapter = new MySqlDataAdapter(command);
                    DataSet ds = new DataSet();
                    adapter.Fill(ds);
                    if (ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0)
                    {
                        minID = Convert.ToInt32(ds.Tables[0].Rows[0]["MIN_TITELNUMMERTRACK_ID"]);
                        maxID = Convert.ToInt32(ds.Tables[0].Rows[0]["MAX_TITELNUMMERTRACK_ID"]);
                        return true;
                    }
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.ToString());
            }

            return false;
        }


        private static bool Exec_MySQL_LOADSUBFINGERIDS(int start, int end, out DataTable dt)
        {
            dt = null;
            // nu zorgen dat computer naam in de database komt en wij deze als ID
            // in deze class opslaan
            try
            {
                using (MySqlConnection conn = CDR.DB_Helper.NewMySQLConnection())
                {
                    if (conn == null)
                    {
                        return false;
                    }

                    MySqlCommand command = new MySqlCommand("SELECT *\r\n" +
                                                            "FROM   TITELNUMMERTRACK_ID AS T1,\r\n" +
                                                            "       SUBFINGERID AS T2\r\n" +
                                                            "WHERE  T1.TITELNUMMERTRACK_ID = T2.TITELNUMMERTRACK_ID\r\n" +
                                                            "AND    T1.TITELNUMMERTRACK_ID BETWEEN " + start.ToString() + " AND " + end.ToString() + "\r\n",
                        conn);
                    command.CommandTimeout = 20 * 60; // max 10 minuten voordat we een timeout genereren

                    MySqlDataAdapter adapter = new MySqlDataAdapter(command);
                    DataSet ds = new DataSet();
                    adapter.Fill(ds);
                    if (ds.Tables.Count > 0)
                    {
                        dt = ds.Tables[0];
                        return true;
                    }
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.ToString());
            }

            return false;
        }

        public static bool Exec_MySQL_LOADFINGERIDS(int start, int end, out DataTable dt)
        {
            dt = null;
            // nu zorgen dat computer naam in de database komt en wij deze als ID
            // in deze class opslaan
            try
            {
                using (MySqlConnection conn = CDR.DB_Helper.NewMySQLConnection())
                {
                    if (conn == null)
                    {
                        return false;
                    }

                    MySqlCommand command = new MySqlCommand("SELECT *\r\n" +
                                                            "FROM   TITELNUMMERTRACK_ID AS T1,\r\n" +
                                                            "       FINGERID AS T2\r\n" +
                                                            "WHERE T1.TITELNUMMERTRACK_ID = T2.TITELNUMMERTRACK_ID\r\n" +
                                                            "AND   T1.TITELNUMMERTRACK_ID BETWEEN " + start.ToString() + " AND " + end.ToString() + "\r\n",
                        conn);

                    command.CommandTimeout = 10 * 60; // max 10 minuten voordat we een timeout genereren

                    MySqlDataAdapter adapter = new MySqlDataAdapter(command);
                    DataSet ds = new DataSet();
                    adapter.Fill(ds);
                    if (ds.Tables.Count > 0)
                    {
                        dt = ds.Tables[0];
                        return true;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }

            return false;
        }

        #endregion
    }
}
