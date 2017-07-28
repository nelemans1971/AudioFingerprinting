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
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using AudioFingerprint;
using AudioFingerprint.Audio;
using AcoustID;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Store;
using MySql.Data.MySqlClient;
using AudioFingerprint.WebService;
using System.Threading;

namespace MatchAudio
{
    class Worker
    {
        private string acoustIDfingerLookupPath;
        private string subFingerLookupPath;
        private AudioEngine audioEngine;

        private string wsAPICDRNL_User = string.Empty;
        private bool retrieveMetadataFromMuziekweb = false;

        /// <summary>
        /// Setup the class, by intializing path's and the audio engine.
        /// Reads the Fingerprint.ini file for the settings
        /// </summary>
        public Worker()
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
            wsAPICDRNL_User = ini.IniReadValue("Muziekweb", "wsAPICDRNL_User", wsAPICDRNL_User);

            // Are we using the muziekweb dataset, Yes than metadata can be requested
            // other datasets can not be used, because ID's do not match
            retrieveMetadataFromMuziekweb = (Exec_MySQL_TITELNUMMERTRACK_ID_COUNT() > 1000000); // this is probally the muziekweb dataset

            AudioFingerprint.Audio.BassLifetimeManager.bass_EMail = ini.IniReadValue("BASS", "bass_EMail", "");
            AudioFingerprint.Audio.BassLifetimeManager.bass_RegistrationKey = ini.IniReadValue("BASS", "bass_RegistrationKey", "");


            acoustIDfingerLookupPath = Path.Combine(luceneIndexPath, acoustIDFingerMap);
            subFingerLookupPath = Path.Combine(luceneIndexPath, subFingerMap);

            AudioFingerprint.Math.SimilarityUtility.InitSimilarityUtility();
            audioEngine = new AudioEngine();
        }


        public void RunAcoustIDTest()
        {
            Console.WriteLine("AcoustID test.");
            if (!System.IO.Directory.Exists(acoustIDfingerLookupPath))
            {
                Console.WriteLine("AcoustID path not found.");
                return;
            }

            Console.WriteLine("Opening index. With larges indexes this can take a while.");
            IndexSearcher indexSubFinger = new IndexSearcher(IndexReader.Open(FSDirectory.Open(new System.IO.DirectoryInfo(acoustIDfingerLookupPath)), true));
            indexSubFinger.Similarity = new CDR.Indexer.SimilarityNoPriority();
            Console.WriteLine("Ready opening index.");

            AcoustIDQuery query = new AcoustIDQuery(indexSubFinger);
            FingerprintAcoustID fsQuery = null;
            Resultset answer = null;

            fsQuery = query.MakeAcoustIDFingerFromAudio(@"..\..\..\Audio\Samples\JK147510-0002-64kpbs.mp3");
            answer = query.MatchAudioFingerprint(fsQuery);
            if (!retrieveMetadataFromMuziekweb)
            {
                ThrowIfInvalidAnswer(answer, "A5D062DE");
            }
            PrintAnswer(answer);
            Console.WriteLine();
        }


        #region SubFingerprint routines

        /// <summary>
        /// Test the subfinger fingerprints, by trying to find a 15 second audio in 3 different bitrates.
        /// 
        /// Needed are:
        /// 1. MySQL database
        /// 2. CreateAudioFingerprint (Fills the MySQL database with fingerprints)
        /// 3. CreateInversedFingerprintIndex (Create a lucene reversed search index, needed to identity a fingerprint)
        /// 4. This program to find a 15 second fragment of audio
        /// </summary>
        public void RunSubFingerTest()
        {
            Console.WriteLine("SubFinger test.");
            if (!System.IO.Directory.Exists(subFingerLookupPath))
            {
                Console.WriteLine("SubFinger path not found.");
                return;
            }

            Console.WriteLine("Opening index. With larges indexes this can take a while.");
            IndexSearcher indexSubFingerLookup = new IndexSearcher(IndexReader.Open(FSDirectory.Open(new System.IO.DirectoryInfo(subFingerLookupPath)), true));
            indexSubFingerLookup.Similarity = new CDR.Indexer.DefaultSimilarityExtended2();
            Console.WriteLine("Ready opening index.");

            SubFingerprintQuery query = new SubFingerprintQuery(indexSubFingerLookup);
            FingerprintSignature fsQuery = null;
            Resultset answer = null;

            fsQuery = CreateSubFingerprintFromAudio(@"..\..\..\Audio\Samples\JK147510-0002-224Sample-45s-60s.mp3");
            answer = query.MatchAudioFingerprint(fsQuery);
            if (!retrieveMetadataFromMuziekweb)
            {
                ThrowIfInvalidAnswer(answer, "A5D062DE");
            }
            PrintAnswer(answer);
            Console.WriteLine();

            fsQuery = CreateSubFingerprintFromAudio(@"..\..\..\Audio\Samples\JK147510-0002-128Sample-45s-60s.mp3");
            answer = query.MatchAudioFingerprint(fsQuery);
            if (!retrieveMetadataFromMuziekweb)
            {
                ThrowIfInvalidAnswer(answer, "A5D062DE");
            }
            PrintAnswer(answer);
            Console.WriteLine();

            fsQuery = CreateSubFingerprintFromAudio(@"..\..\..\Audio\Samples\JK147510-0002-64Sample-45s-60s.mp3");
            answer = query.MatchAudioFingerprint(fsQuery);
            if (!retrieveMetadataFromMuziekweb)
            {
                ThrowIfInvalidAnswer(answer, "A5D062DE");
            }
            PrintAnswer(answer);
            Console.WriteLine();
        }

        /// <summary>
        /// Read a audio file (remember for sub fingerprints no more than 15 seconds)
        /// Downsample it to mono and 5512Hz
        /// Use the samples to create a fingerprint
        /// 
        /// return a fingerprint signature.
        /// </summary>
        private FingerprintSignature CreateSubFingerprintFromAudio(string filename)
        {
            DateTime startTime = DateTime.Now;
            SpectrogramConfig spectrogramConfig = new DefaultSpectrogramConfig();

            // First read audio file and downsample it to mono 5512hz
            AudioSamples samples = audioEngine.ReadMonoFromFile(filename, spectrogramConfig.SampleRate, 0, -1);
            Console.WriteLine(string.Format("Resample tot mono {0}hz : {1:##0.000} sec.", spectrogramConfig.SampleRate, (DateTime.Now - startTime).TotalMilliseconds / 1000));

            startTime = DateTime.Now;
            // Now slice the audio in chunks seperated by 11,6 ms (5512hz 11,6ms = 64 samples!)
            // An with length of 371ms (5512kHz 371ms = 2048 samples [rounded])
            FingerprintSignature fsQuery = audioEngine.CreateFingerprint(samples, spectrogramConfig);
            Console.WriteLine(string.Format("Hashing audio to fingerprint : {0:##0.000} sec.", (DateTime.Now - startTime).TotalMilliseconds / 1000));

            return fsQuery;
        }

        /// <summary>
        /// Some radio channels do audio stretching. (Letting the audio run faster or slower, then it orginal was).
        /// The algoritme for the fingerprint, can't cope with it. So you have to slow ro speed to audio chunk back to it's 
        /// orginal speed before matching.
        /// 
        /// eg. Radio538 in the Netherlands speeds up it sound with a factor of 1.4
        /// To bring the audio back for matching you enter a stretchRate of -1.4
        /// 
        /// Finding a stretchrate is trying different values on a know audio fragment until you get the best 
        /// audio recognition.
        /// </summary>
        private FingerprintSignature CreateSubFingerprintFromAudioWithTimeStretch(string filename, float stretchRate)
        {
            DateTime startTime = DateTime.Now;

            AudioSamples samples = audioEngine.TimeStretch(filename, stretchRate);

            SpectrogramConfig spectrogramConfig = new DefaultSpectrogramConfig();
            // First read audio file and downsample it to mono 5512hz
            samples = audioEngine.Resample(samples.Samples, samples.SampleRate, 2, 5512);
            Console.WriteLine(string.Format("Resample tot mono {0}hz : {1:##0.000} sec.", spectrogramConfig.SampleRate, (DateTime.Now - startTime).TotalMilliseconds / 1000));

            startTime = DateTime.Now;
            // Now slice the audio in chunks seperated by 11,6 ms (5512hz 11,6ms = 64 samples!)
            // An with length of 371ms (5512kHz 371ms = 2048 samples [rounded])
            FingerprintSignature fsQuery = audioEngine.CreateFingerprint(samples, spectrogramConfig);
            Console.WriteLine(string.Format("Hashing audio to fingerprint : {0:##0.000} sec.", (DateTime.Now - startTime).TotalMilliseconds / 1000));

            return fsQuery;
        }

        #endregion

        #region Some info and test stuff

        private void ThrowIfInvalidAnswer(Resultset answer, string referenceIDs)
        {
            if (answer == null)
            {
                throw new Exception("var is null.");
            }

            if (referenceIDs.Length == 0)
            {
                return;
            }

            bool found = false;
            foreach (ResultEntry re in answer.ResultEntries)
            {
                foreach (string referenceID in referenceIDs.Split(';'))
                {
                    if (re.Reference.ToString() == referenceID)
                    {
                        found = true;
                        break;
                    }
                }
                if (found)
                {
                    break;
                }
            }
            if (!found)
            {
                throw new Exception(string.Format("Invalid reference. Excpected '{0}'.", referenceIDs));
            }
        }

        private ManualResetEvent signalEvent = new ManualResetEvent(false);
        private void PrintAnswer(Resultset result)
        {
            if (result != null)
            {
                Console.WriteLine("======================================================================");
                Console.WriteLine("Algorithm: " + result.Algorithm.ToString());
                Console.Write("Stats: ");
                Console.Write("Total=" + (result.QueryTime.TotalMilliseconds / 1000).ToString("#0.000") + "s");
                Console.Write(" | FingerQry=" + (result.FingerQueryTime.TotalMilliseconds / 1000).ToString("#0.000") + "s");
                Console.Write(" | FingerLD=" + (result.FingerLoadTime.TotalMilliseconds / 1000).ToString("#0.000") + "s");
                Console.Write(" | Match=" + (result.MatchTime.TotalMilliseconds / 1000).ToString("#0.000") + "s");
                Console.WriteLine();
                Console.WriteLine();

                foreach (ResultEntry item in result.ResultEntries)
                {
                    Song song = new Song();
                    if (RetrieveMetadataFromMuziekweb(item.Reference.ToString()))
                    {
                        signalEvent.Reset();
                        WSRecognize detectTask = new WSRecognize();
                        string reference = item.Reference.ToString();
                        if (!retrieveMetadataFromMuziekweb && MapCRC32ToMuziekwebReference(reference).Length > 0)
                        {
                            reference = MapCRC32ToMuziekwebReference(reference);
                        }
                        detectTask.RetrieveMetaDataMuziekweb(reference, song, REST_ResultMetaDataMuziekweb);
                        // wait until metadata is retrieved
                        signalEvent.WaitOne();
                    }

                    Console.WriteLine("SearchPlan  : " + item.SearchStrategy.ToString());
                    Console.WriteLine("Reference   : " + item.Reference.ToString());
                    // AcoustID is for complete track so position in track is pointless
                    if (item.TimeIndex >= 0)
                    {
                        Console.WriteLine("Position    : " + (item.Time.TotalMilliseconds / 1000).ToString("#0.000") + " sec");
                    }
                    else
                    {
                        Console.WriteLine("Position    : Match on complete track");
                    }
                    if (result.Algorithm == FingerprintAlgorithm.AcoustIDFingerprint)
                    {
                        Console.WriteLine(string.Format("Match perc. : {0}%", item.Similarity));
                    }
                    else
                    {
                        Console.WriteLine("BER         : " + item.Similarity.ToString());
                    }
                    if (!string.IsNullOrEmpty(song.AlbumTrackID))
                    {
                        Console.WriteLine(string.Format("Album : {0} / {1}", song.Album.AlbumTitle, PerformerList(song.Album.Performers)));
                        Console.WriteLine(string.Format("Song  : {0} / {1}", song.SongTitle, PerformerList(song.Performers)));
                    }

                    Console.WriteLine();
                } //foreach
                Console.WriteLine("======================================================================");
            }
        }

        private bool RetrieveMetadataFromMuziekweb(string reference)
        {
            if (retrieveMetadataFromMuziekweb && !wsAPICDRNL_User.Equals(string.Empty))
            {
                return true;
            }

            if (MapCRC32ToMuziekwebReference(reference).Length > 0)
            {
                return true;
            }

            return false;
        }

        private string MapCRC32ToMuziekwebReference(string reference)
        {
            if (!string.IsNullOrEmpty(reference))
            {
                switch (reference.ToUpper())
                {
                    case "E270180E": // JK147510-0001
                        return "74829A1C77B228BF";
                    case "A5D062DE": // JK147510-0002
                        return "302FF4B87C6B606";
                    case "98B04B6E": // JK147510-0003 
                        return "E4475D91DB27D51B";
                    case "2A90977E": // JK147510-0004
                        return "73A9C151F8A6FD2B";
                    case "17F0BECE": // JK147510-0005
                        return "27B213723A828870";
                    case "5050C41E": // JK147510-0006
                        return "37CA54A2D6832526";
                    case "6D30EDAE": // JK147510-0007
                        return "82ED646BD7DA842A";
                    case "EF607A7F": // JK147510-0008
                        return "FC3E0F7E9073D929";
                    case "D20053CF": // JK147510-0009
                        return "7E1351D8ABD1456D";
                    case "144CE21B": // JK147510-0010
                        return "3552D854447400F2";
                } //switch
            }

            return string.Empty;
        }

        private string PerformerList(List<Performer> performerList)
        {
            StringBuilder sb = new StringBuilder(400);
            foreach (Performer performer in performerList)
            {
                if (sb.Length > 0)
                {
                    sb.Append(" & ");
                }
                sb.Append(performer.PresentationName);
            } //foreach

            return sb.ToString();
        }

        private void REST_ResultMetaDataMuziekweb(object sender, bool success, ResultSongs resultSongs, object userState = null)
        {
            try
            {
                Song song = userState as Song;
                if (resultSongs != null && resultSongs.Songs.Count > 0)
                {
                    // Copy song data to userstate
                    song.ContextID = resultSongs.Songs[0].ContextID;

                    song.Score = resultSongs.Songs[0].Score;
                    song.TrackNumber = resultSongs.Songs[0].TrackNumber;
                    song.Album = resultSongs.Songs[0].Album; // shallow copy
                    song.AlbumTrackID = resultSongs.Songs[0].AlbumTrackID;
                    song.SongTitle = resultSongs.Songs[0].SongTitle;
                    song.SongTitle_Link = resultSongs.Songs[0].SongTitle_Link;
                    song.UniformTitle = resultSongs.Songs[0].UniformTitle;
                    song.UniformTitle_Link = resultSongs.Songs[0].UniformTitle_Link;
                    song.PlayTimeInSec = resultSongs.Songs[0].PlayTimeInSec;
                    song.Performers = resultSongs.Songs[0].Performers;// shallow copy
                }
            }
            finally
            {
                // Signal metadata is retrieved
                signalEvent.Set();
            }
        }


        #endregion

        public static int Exec_MySQL_TITELNUMMERTRACK_ID_COUNT()
        {
            try
            {
                using (MySqlConnection conn = CDR.DB_Helper.NewMySQLConnection())
                {
                    MySqlCommand command = new MySqlCommand();
                    command.Connection = conn;
                    command.CommandType = System.Data.CommandType.Text;
                    command.CommandText = "SELECT COUNT(*) FROM TITELNUMMERTRACK_ID WHERE TITELNUMMERTRACK_ID > 0";
                    command.CommandTimeout = 60; // max 5 minuten voordat we een timeout genereren

                    int count = Convert.ToInt32(command.ExecuteScalar());
                    return count;
                }
            }
            catch { }

            return 0;
        }

    }
}
