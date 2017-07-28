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
using System.IO;
using System.Text;
using System.Threading;
using Lucene.Net.Store;
using Lucene.Net.Documents;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Index;
using Lucene.Net.Search;
using CDR;
using CDR.Logging;
using Lucene.Net.Support;

namespace RadioChannelWebService
{

    public class LuceneIndex
    {
        enum IndexRequests { IndexNoRequests, IndexReloadIndex };

        private int lockCount = 0;
        private IndexRequests indexAction = IndexRequests.IndexNoRequests;

        private string indexBasePath = string.Empty;
        private string indexName = string.Empty;

        private IndexSearcher index = null;

        public LuceneIndex(string path, string indexName)
        {
            indexBasePath = path;
            this.indexName = indexName;

            OpenIndex();
        }

        /// <summary>
        /// Complete filename to lucene index
        /// </summary>
        public string IndexName
        {
            get
            {
                return Path.Combine(indexBasePath, indexName);
            }
        }

        /// <summary>
        /// Checks if the files exists for an index
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static bool IndexIsValid(string path)
        {
            try
            {
                return true;
                string[] files = System.IO.Directory.GetFiles(path, "*.cfs", SearchOption.TopDirectoryOnly);
                if (files.Length >= 1)
                {
                    files = System.IO.Directory.GetFiles(path, "*.gen", SearchOption.TopDirectoryOnly);
                    if (files.Length >= 1)
                    {
                        files = System.IO.Directory.GetFiles(path, "*.", SearchOption.TopDirectoryOnly);
                        if (files.Length >= 1)
                        {
                            return true;
                        }
                    }
                }
            }
            catch { }

            return false;
        }

        /// <summary>
        /// Open de Lucene index
        /// </summary>
        /// <returns></returns>
        protected virtual bool OpenIndex()
        {
            CDRLogger.FileLogger.LogInfo(String.Format("OpenIndex(): begin using path {0}", IndexName));
            bool result = false;

            if (index != null)
            {
                CloseIndex();
            }

            try
            {
                index = new IndexSearcher(IndexReader.Open(FSDirectory.Open(new System.IO.DirectoryInfo(indexBasePath)), true));
                index.SetDefaultFieldSortScoring(true, true);

                result = true;
            }
            catch (Exception e)
            {
                CDRLogger.Logger.LogInfo("CloseIndex(): " + e.Message);
                result = false;
                // voor de zekerheid!
                CloseIndex();
            }

            CDRLogger.FileLogger.LogInfo("OpenIndex(): end");
            return result;
        }

        /// <summary>
        /// Sluit de lucene index. Belangrijk moet altijd worden aangeroepen.
        /// </summary>
        public virtual void CloseIndex()
        {
            CDRLogger.FileLogger.LogInfo(String.Format("CloseIndex(): begin ({0})", IndexName));

            if (index != null)
            {
                CDRLogger.FileLogger.LogInfo(String.Format("CloseIndex(): Index was open and will now be closed. ({0})", IndexName));

                try
                {
                    // This is important otherwhise index won't be closed!
                    index.IndexReader.Dispose();

                    index.Dispose();
                    index = null;
                    Thread.Sleep(100);
                    GC.WaitForPendingFinalizers();
                }
                catch (Exception e)
                {
                    CDRLogger.Logger.LogError(e);
                }
            }

            CDRLogger.FileLogger.LogInfo(String.Format("CloseIndex(): end ({0})", IndexName));
        }


        /// <summary>
        /// Stel een verzoek tot opnieuw laden van de index. Als er gelijk aan
        /// kan worden voldaan dan wordt het gelijk uitgevoerd.
        /// </summary>
        public void ReloadIndex(string newIndexBasePath)
        {
            CDRLogger.FileLogger.LogInfo(String.Format("ReloadIndex(): ReloadIndex(\"{0}\") lockCount={1}", newIndexBasePath, lockCount));
            CDRLogger.FileLogger.LogInfo(String.Format("ReloadIndex(): Old path {0}", indexBasePath));

            // Als lockCount == 0 dan -1 (en we kunnen dan de index reloaden!
            // als hij -1 is dan zijn we al bezig met het herladen van de indexen
            while (Interlocked.CompareExchange(ref lockCount, -1, 0) != -1)
            {
                // wacht 10 milliseconden en probeer dan opnieuw
                // lock te pakken te krijgen
                Thread.Sleep(10);
            }

            // Geef aan dat we de index opnieuw willen laden!
            try
            {
                indexAction = IndexRequests.IndexReloadIndex;

                CloseIndex();

                if (!string.IsNullOrEmpty(newIndexBasePath))
                {
                    // Nu gooien we de "oude" index weg en verplaatsen we de nieuwe naar deze plek
                    ClearAndRemoveFolder(indexBasePath);
                    GC.WaitForPendingFinalizers();
                    System.IO.Directory.Move(newIndexBasePath, indexBasePath);
                }

                // Open index weer
                OpenIndex();
            }
            finally
            {
                // Zet lock weer op 0 zodat het verhaal weer opnieuw kan beginnen
                Interlocked.Exchange(ref lockCount, 0);
                indexAction = IndexRequests.IndexNoRequests;
            }

            CDRLogger.FileLogger.LogInfo(String.Format("ReloadIndex(): end ReloadIndex(\"{0}\")", indexBasePath));
        }
        
        private void ClearAndRemoveFolder(string folderName)
        {
            if (string.IsNullOrEmpty(folderName) || folderName.Trim().Length <= 3)
            {
                return;
            }

            int retryCount = 3;
            try
            {
                DirectoryInfo dir = new DirectoryInfo(folderName);
                foreach (FileInfo fi in dir.GetFiles())
                {
                    retryCount = 3;
                    while (retryCount > 0)
                    {
                        try
                        {
                            fi.IsReadOnly = false;
                            fi.Delete();
                            retryCount = 0;
                        }
                        catch
                        {
                            GC.WaitForPendingFinalizers();
                            retryCount--;
                        }
                    } //while
                }

                foreach (DirectoryInfo di in dir.GetDirectories())
                {
                    ClearAndRemoveFolder(di.FullName);
                    di.Delete();
                }

                retryCount = 3;
                while (retryCount > 0)
                {
                    try
                    {
                        System.IO.Directory.Delete(folderName);
                    }
                    catch
                    {
                        GC.WaitForPendingFinalizers();
                        retryCount--;
                    }
                } //while
            }
            catch
            {
            }
        }


        public int LockCount
        {
            get
            {
                return lockCount;
            }
        }

        public void Lock()
        {
            // Je kunt geen lock krijgen als lockCount < 0 is (we zijn dan in een reload van de indexen bezig)
            while (Interlocked.CompareExchange(ref lockCount, 0, 0) < 0)
            {
                Thread.Sleep(10);
            }
            Interlocked.Increment(ref lockCount);
        }

        public void UnLock()
        {
            Interlocked.Decrement(ref lockCount);
        }

        public IndexSearcher Index
        {
            get
            {
                return index;
            }
        }

    }

    static public class LuceneIndexes
    {
        private static string luceneIndexPath = @".\DB\";
        private static string dbSubFingerMap = "SubFingerLookup";

        // Dit zijn de indexen die we beschikbaar hebben.
        private static bool initialized = false;
        private static object criticalSection = new object();

        private static string activeTitelDBPath = null;

        private static LuceneIndex indexFinger = null;
        private static LuceneIndex indexSubFinger = null;

        static LuceneIndexes()
        {
            string IniPath = CDR.DB_Helper.FingerprintIniFile;
            if (System.IO.File.Exists(IniPath))
            {
                try
                {
                    CDR.Ini.IniFile ini = new CDR.Ini.IniFile(IniPath);
                    luceneIndexPath = ini.IniReadValue("Program", "LuceneIndexPath", luceneIndexPath);
                    dbSubFingerMap = ini.IniReadValue("Program", "SubFingerMap", dbSubFingerMap);
                }
                catch { }
            }
            dbSubFingerMap = Path.GetFullPath(Path.Combine(luceneIndexPath, dbSubFingerMap));
        }

        public static void Initialize()
        {
            lock (criticalSection)
            {
                if (!initialized)
                {
                    initialized = true;

                    // Reload index bepaalt de active titel database (en probeert de IndexSearchers 
                    // opnieuw te laden dit laatste gebeurd niet omdat ze nog nooit geopened zijn en 
                    // dat is hier ook de bedoeling)
                    ReloadSubFingerIndex("");
                }
            }
        }

        public static void Close()
        {
            if (indexFinger != null)
            {
                indexFinger.CloseIndex();
                indexFinger = null;
            }

            if (indexSubFinger != null)
            {
                indexSubFinger.CloseIndex();
                indexSubFinger = null;
            }
        }
        
        /// <summary>
        /// De Datum+Tijd van de active index. Hiermee bepalen we of we moeten gaan updaten of dat we de index
        /// juist "sharen".
        /// Er staat als het goed is een torrent "bestand" in de hoofdmap waarin datum+tijd is geencodeerd. Kan dit bestand
        /// niet gevonden worden dan vallen we terug op de create date van "orderInfo" index (Deze wordt als 
        /// een van de laatste aangemaakt). We kijken dan naar de bestand naam ".\Label\OrderInfo\segments.gen" 
        /// die altijd bij indexen aanwezig is.
        /// </summary>
        public static DateTime DateTimeActiveIndex
        {
            get
            {
                return DateTime.MinValue;
            }
        }

        /// <summary>
        /// De Datum+Tijd van de inactive index. Hiermee bepalen we of we moeten gaan updaten of dat we de index
        /// juist "sharen".
        /// Er staat als het goed is een "bestand" in de hoofdmap waarin datum+tijd is geencodeerd. Kan dit bestand
        /// niet gevonden worden dan vallen we terug op de create date van "orderInfo" index (Deze wordt als 
        /// een van de laatste aangemaakt). We kijken dan naar de bestand naam ".\Label\OrderInfo\segments.gen" 
        /// die altijd bij indexen aanwezig is.
        /// </summary>
        public static DateTime DateTimeInactiveIndex
        {
            get
            {
                return DateTime.MinValue;
            }
        }

        public static string SubFingerprintLocation
        {
            get
            {
                return dbSubFingerMap;
            }
        }

        public static string ActivePath
        {
            get
            {
                return luceneIndexPath;
            }
        }

        public static string InactivePath
        {
            get
            {
                return luceneIndexPath;
            }
        }

        public static void SwitchIndexes()
        {
        }

        public static void ReloadSubFingerIndex(string newIndexPath)
        {
            lock (criticalSection)
            {
                Initialize();

                CDRLogger.FileLogger.LogInfo("ReloadIndexes(): Status open indexen:");

                if (indexSubFinger != null)
                {
                    CDRLogger.FileLogger.LogInfo("ReloadIndexes(): - indexSubFinger");
                    indexSubFinger.ReloadIndex(newIndexPath);
                }
                CDRLogger.FileLogger.LogInfo("ReloadIndexes(): Einde status rapport open indexen");
            }
        }

        public static LuceneIndex IndexSubFingerprint
        {
            get
            {
                if (!initialized)
                {
                    Initialize();
                }

                if (indexSubFinger == null)
                {
                    lock (criticalSection)
                    {
                        indexSubFinger = new LuceneIndex(dbSubFingerMap, "SubFingerprint");
                    }
                }

                return indexSubFinger;
            }
        }
    }
}
