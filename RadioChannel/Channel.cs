#region License
// Copyright (c) 2015-2017 Stichting Centrale Discotheek Rotterdam.
// 
// website: https://www.muziekweb.nl
// e-mail:  info@muziekweb.nl
//
// This code is under MIT licence, you can find the complete file here: 
// LICENSE.MIT
#endregion
#define TIMEQUERYINFO
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AudioFingerprint.Audio;
using AudioFingerprint.WebService;
using MySql.Data.MySqlClient;
using Un4seen.Bass.Misc;


namespace RadioChannel
{
    class Channel
    {
        private object lockObject = new object();
        private IceCastStream radioStream = null;
        private DateTime dtLastEvent = DateTime.MaxValue;
        private float[] audioSample = null; // 15 seconden 44100Hz stereo
        private int audioOffset = 0;
        private WSRecognize detectTask = null;
        private int radioChannel = -1;
        private string radioName = "";
        private string radioCode = "";

        private ConcurrentDictionary<string, DetectSong> dict = null;
        private DateTime waitUntil = DateTime.MinValue;

        private bool doWriteChunksToWAVFile = false;
        private bool doAudioOn = false;

        private bool doTimeStretching = false;
        private float timeStretchRateFactor = 0.0f;
        private string webServiceCall = "DEFAULT";
        private string wsAPICDRNL_User = string.Empty;
        private bool retrieveMetadataFromMuziekweb = false;


        public void RunChannel(string radioChannelCode)
        {
            DataRow row;
            if (!Exec_MySQL_RADIOCHANNEL_S(radioChannelCode, out row))
            {
                Console.WriteLine("No radiochannel available. Please set correct 'RadioCode' in ini file");
                return;
            }

            dict = new ConcurrentDictionary<string, DetectSong>();

            radioName = row["RADIONAME"].ToString();
            radioCode = row["NAMECODE"].ToString();
            radioChannel = Convert.ToInt32(row["RADIOCHANNEL_ID"]);
            GetSettings();
            SetWindowTitle();

            try
            {
                CDR.Ini.IniFile ini = new CDR.Ini.IniFile(CDR.DB_Helper.FingerprintIniFile);
                wsAPICDRNL_User = ini.IniReadValue("Muziekweb", "wsAPICDRNL_User", wsAPICDRNL_User);
            }
            catch { }

            // Are we using the muziekweb dataset, Yes than metadata can be requested
            // other datasets can not be used, because ID's do not match
            retrieveMetadataFromMuziekweb = (Exec_MySQL_TITELNUMMERTRACK_ID_COUNT() > 1000000); // this is probally the muziekweb dataset


            Console.WriteLine(string.Format("Listing to '{0}'.", radioName));

            // Kies een tijd uit waarbij de minuut in het uur ('s nachts 2 uur) random is gekozen 
            // (zodat ze niet allemaal tegelijktijd stoppen met werken)
            Random random = new Random();
            DateTime dtStopListening = DateTime.Now.Date.Add(new TimeSpan(1, 2, random.Next(0, 59), 0, 0));

            SetupStream(out radioStream, row["URL"].ToString());
            try
            {
                bool stop = false;
                do
                {
                    if (Console.KeyAvailable)
                    {
                        ConsoleKeyInfo key = Console.ReadKey(true);
                        if (key.KeyChar == '\r')
                        {
                            stop = true;
                            break;
                        }
                        else if (char.ToUpper(key.KeyChar) == 'W')
                        {
                            doWriteChunksToWAVFile = !doWriteChunksToWAVFile;
                            SetWindowTitle();
                        }
                        else if (char.ToUpper(key.KeyChar) == 'A')
                        {
                            doAudioOn = !doAudioOn;
                            SetWindowTitle();
                            radioStream.Listen = doAudioOn;
                        }
                    }

                    Thread.Sleep(100);
                    if ((DateTime.Now - dtLastEvent).TotalSeconds > 30)
                    {
                        // Het is telang geleden dat we enig leven van de icestream class hebben gekregen
                        // Kill het en maak een nieuwe aan.
                        dtLastEvent = DateTime.MaxValue;
                        Console.WriteLine();
                        Console.WriteLine(radioName + ": Killing app and restarting it");

                        Program.StoreConsoleWindowPosition(radioChannelCode);

                        // terminate this process and restart it (to avoid block/lock problems)
                        System.Diagnostics.Process.Start(System.Reflection.Assembly.GetExecutingAssembly().Location, GetCommandParameters());
                        stop = true;
                        break;
                    }

                    if ((DateTime.Now - dtStopListening).TotalSeconds > 0)
                    {
                        // we stoppen zonder stop te zetten!
                        System.Diagnostics.Process.Start(System.Reflection.Assembly.GetExecutingAssembly().Location, GetCommandParameters());
                        break;
                    }
                } while (!stop);

                CleanUpDict(true);
            }
            finally
            {
                if (radioStream != null)
                {
                    radioStream.Stop(); // close is the same
                    radioStream = null;
                }
            }
        }

        private string GetCommandParameters()
        {
            int count = 0;
            StringBuilder sb = new StringBuilder();
            foreach (string s in Environment.GetCommandLineArgs())
            {
                if (count > 0)
                {
                    if (sb.Length > 0)
                    {
                        sb.Append(' ');
                    }
                    sb.Append(s);
                }
                count++;
            }

            return sb.ToString();
        }

        private void SetupStream(out IceCastStream radioStream, string url)
        {
            radioStream = new IceCastStream(url, false);
            radioStream.Start();
            radioStream.OnStreamUpdate += new StreamUpdate(IceCastStreamUpdate);
            dtLastEvent = DateTime.Now;
        }

        private void SetWindowTitle()
        {
            Console.Title = "Radio: " + radioName + " | [W]avFile=" + Convert.ToInt16(doWriteChunksToWAVFile) + " | [A]udio=" + Convert.ToInt16(doAudioOn);
        }

        /// <summary>
        /// Retrieve special settinggs for radiochannel
        /// </summary>
        private void GetSettings()
        {
            string iniFilename = Path.ChangeExtension(System.Reflection.Assembly.GetExecutingAssembly().Location, ".ini");
            if (File.Exists(iniFilename))
            {
                System.Globalization.NumberFormatInfo nfi = new System.Globalization.CultureInfo("en-US", false).NumberFormat;
                nfi.CurrencySymbol = "€";
                nfi.CurrencyDecimalDigits = 2;
                nfi.CurrencyDecimalSeparator = ".";
                nfi.NumberGroupSeparator = "";
                nfi.NumberDecimalSeparator = ".";

                CDR.Ini.IniFile ini = new CDR.Ini.IniFile(iniFilename);
                doTimeStretching = (ini.IniReadValue(radioCode, "Preprocessor1", "").ToUpper() == "TIMESTRETCH");
                timeStretchRateFactor = Convert.ToSingle(ini.IniReadValue(radioCode, "Preprocessor1Parameter1", "0.0"), nfi);
                webServiceCall = ini.IniReadValue(radioCode, "WebServiceCall", "DEFAULT");
            }
        }

        /// <summary>
        /// We get an event with 1 second of data (PCM 44100hz)
        /// </summary>
        private void IceCastStreamUpdate(object sender, StreamUpdateEventArgs data)
        {
            dtLastEvent = DateTime.Now;

            if (audioSample == null)
            {
                audioSample = new float[44100 * 2 * 15];
                audioOffset = 0;
            }

            int len = data.Data.Length;
            if (audioOffset + len > audioSample.Length)
            {
                len = audioSample.Length - audioOffset;
            }

            Buffer.BlockCopy(data.Data, 0, audioSample, audioOffset * 4, len * 4);
            audioOffset += len;

            if (audioOffset == audioSample.Length)
            {
                // Write wav file to disk
                if (doWriteChunksToWAVFile)
                {
                    string filename = string.Format("{0}-{1:yyyy-MM-dd}#{1:HHmmss}#.wav", radioName, DateTime.Now);
                    WaveWriter ww = new WaveWriter(filename, 2, 44100, 16, true);
                    ww.Write(audioSample, audioSample.Length * 4);
                    ww.Close();
                    //ww.Dispose(); // this removes the file                
                }

                DetectAudio(audioSample, 44100, 2);
                audioSample = null;
                audioOffset = 0;
            }
        }

        private void DetectAudio(float[] inputAudioSample, int inputSampleRate, int inputChannels)
        {
            TimeSpan wait = (DateTime.Now - waitUntil);
            if (wait.TotalDays < 0 && wait.TotalSeconds < 0)
            {
                return;
            }

            // Run separeted task, ignore when one is allready running
            lock (lockObject)
            {
                // if detection task is runnign don't run a new one (otherwhise als er al een detectie draait dan niet verder gaan
                if (detectTask != null)
                {
                    return;
                }

                detectTask = new WSRecognize();
            } //lock


            Task task = Task.Factory.StartNew(() =>
            {
                using (AudioEngine ae = new AudioEngine())
                {
                    AudioSamples audio = new AudioSamples();
                    audio.Channels = inputChannels;
                    audio.SampleRate = inputSampleRate;
                    audio.Samples = inputAudioSample;
                    audio.Origin = "MEMORY";

                    if (doTimeStretching)
                    {
                        Console.WriteLine("Timestretching with " + timeStretchRateFactor.ToString("#0.00") + "f");
                        audio = ae.TimeStretch(audio.Samples, audio.SampleRate, audio.Channels, timeStretchRateFactor);
                    }
                    audio = ae.Resample(audio.Samples, audio.SampleRate, audio.Channels, 5512);
                    FingerprintSignature fsQuery = ae.CreateFingerprint(audio, SpectrogramConfig.Default);
                    switch (webServiceCall)
                    {
                        case "SLOW":
                            detectTask.DetectAudioFragmentSlow(fsQuery, null, REST_ResultFingerDetect);
                            break;
                        default:
                            detectTask.DetectAudioFragmentFast(fsQuery, null, REST_ResultFingerDetect);
                            break;
                    }
                    //detectTask.DetectAudioFragment(fsQuery, null, REST_ResultFingerDetect);
                } //using                
            });
        }

        private void REST_ResultFingerDetect(object sender, bool success, ResultFingerprintRecognition resultRecognitions, object userState = null)
        {
            if (success && resultRecognitions != null && resultRecognitions.FingerTracks.Count > 0)
            {
                SongInfoRetrieve(resultRecognitions);
                // Leave detectTask intact, because we need to do a second call to get the metadata
                return;
            }

            try
            {
                if (resultRecognitions != null)
                {
                    string line = string.Empty;
                    switch (resultRecognitions.RecognizeCode)
                    {
                        case RecognizeCode.EXCEPTION:
                            line = "Error: " + resultRecognitions.RecognizeResult;
                            break;
                        case RecognizeCode.TIMEOUT:
                            line = "Timeout. Waiting 30 seconds before submitting detect query again.";
                            waitUntil = DateTime.Now.AddSeconds(30);
                            break;
                        case RecognizeCode.SERVERNOTFOUND:
                            line = "Server not found. Waiting 30 seconds before submitting detect query again.";
                            waitUntil = DateTime.Now.AddSeconds(30);
                            break;
                        case RecognizeCode.OK:
                            if (resultRecognitions.FingerTracks.Count == 0)
                            {
                                line = "Unknown song. Q=" + (resultRecognitions.TimeStatistics.TotalQueryTime.TotalMilliseconds / 1000).ToString("#0.000");
                            }
                            break;
                        default:
                            line = "Unknown error (" + resultRecognitions.RecognizeResult + ").";
                            break;
                    } //switch
                    if (line.Length > 0)
                    {
                        Console.WriteLine(line);
                    }

#if TIMEQUERYINFO
                    Console.Write("  QTotal=" + (resultRecognitions.TimeStatistics.TotalQueryTime.TotalMilliseconds / 1000).ToString("#0.000"));
                    Console.Write(" QSubF=" + (resultRecognitions.TimeStatistics.SubFingerQueryTime.TotalMilliseconds / 1000).ToString("#0.000"));
                    Console.Write(" QFinger=" + (resultRecognitions.TimeStatistics.FingerLoadTime.TotalMilliseconds / 1000).ToString("#0.000"));
                    Console.Write(" QMatch=" + (resultRecognitions.TimeStatistics.MatchTime.TotalMilliseconds / 1000).ToString("#0.000"));
                    Console.WriteLine();
#endif
                    Console.WriteLine();
                }

                CleanUpDict(false);
            }
            finally
            {
                // Release detetctask so new one can be initiated
                lock (lockObject)
                {
                    detectTask = null;
                }
            }
        }


        /// <summary>
        /// walk through the dictionary and clean it ip
        /// if forceAll = true then all entries are checked
        /// </summary>
        private void CleanUpDict(bool forceAll = false)
        {
            List<string> delKeys = new List<string>();

            foreach (KeyValuePair<string, DetectSong> entry in dict)
            {
                DetectSong dSong = entry.Value;

                // Einde van Song?
                if ((DateTime.Now - dSong.EndTime).TotalSeconds >= (3 * 60) || forceAll)
                {
                    // Ja, Nu checken of het ook een goede detectie was

                    if ((dSong.EndTime - dSong.StartTime).TotalSeconds >= (int)(dSong.Song.PlayTimeInSec / 2) || (dSong.EndTime - dSong.StartTime).TotalSeconds > 30)
                    {
                        Console.WriteLine();
                        Console.WriteLine("-----------------------------------------------------------------------------");
                        Console.WriteLine("Song     : " + SongInfo(dSong.Song));
                        Console.WriteLine("Playtime : " + dSong.StartTime.ToString("HH:mm:ss") + " - " + dSong.EndTime.ToString("HH:mm:ss"));
                        Console.WriteLine("-----------------------------------------------------------------------------");
                        Console.WriteLine();

                        string performerLink = "";
                        if (dSong.Song.Performers.Count > 0)
                        {
                            performerLink = dSong.Song.Performers[0].Link;
                        }
                    }
                    else
                    {
                        Console.WriteLine("Skipped: " + SongInfo(dSong.Song));

                        Console.WriteLine("StartTime=" + dSong.StartTime.ToString());
                        Console.WriteLine("EndTime=" + dSong.EndTime.ToString());
                        Console.WriteLine("PlayTimeInSec=" + dSong.Song.PlayTimeInSec.ToString());
                        Console.WriteLine();
                        Console.WriteLine();
                    }

                    delKeys.Add(entry.Key);
                }
            } //foreach

            foreach (string key in delKeys)
            {
                DetectSong dInfo;
                dict.TryRemove(key, out dInfo);
            } //foreach
        }

        private void SongInfoRetrieve(ResultFingerprintRecognition resultRecognitions)
        {
            if (RetrieveMetadataFromMuziekweb(resultRecognitions.FingerTracks[0].FingerTrackReference))
            {
                detectTask.RetrieveMetaDataMuziekweb(resultRecognitions.FingerTracks[0].FingerTrackReference, resultRecognitions, REST_ResultMetaDataMuziekweb);
            }
            else
            {
                // No metadata just show what we got
                REST_ResultMetaDataMuziekweb(this, false, null, resultRecognitions);
            }
        }

        private bool RetrieveMetadataFromMuziekweb(string reference)
        {
            if (retrieveMetadataFromMuziekweb && !wsAPICDRNL_User.Equals(string.Empty))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Called when metadata request returns (or simulated in SongInfoRetrieve
        /// to skip metadata request)
        /// </summary>
        private void REST_ResultMetaDataMuziekweb(object sender, bool success, ResultSongs resultSongs, object userState = null)
        {
            try
            {
                ResultFingerprintRecognition resultRecognitions = userState as ResultFingerprintRecognition;

                Console.WriteLine("Detected: " + resultRecognitions.FingerTracks[0].FingerTrackReference.ToString() + " Q=" + (resultRecognitions.TimeStatistics.TotalQueryTime.TotalMilliseconds / 1000).ToString("#0.000"));                
                if (resultSongs != null && resultSongs.Songs.Count > 0)
                {
                    Console.WriteLine("MetaData: " + SongInfo(resultSongs.Songs[0]));
                }
                Console.Write("  P=" + resultRecognitions.FingerTracks[0].SearchStrategy.SearchName + " (I=" + resultRecognitions.FingerTracks[0].SearchStrategy.SearchIteration.ToString() + ")");
                Console.Write(" | BER=" + resultRecognitions.FingerTracks[0].BER.ToString());
                Console.Write(" | Index=" + resultRecognitions.FingerTracks[0].SearchStrategy.IndexNumberInMatchList.ToString() + " | HitCount=" + resultRecognitions.FingerTracks[0].SearchStrategy.SubFingerCountHitInFingerprint.ToString());
                Console.WriteLine();
            }
            finally
            {
                // Release detecttask so new one can be initiated
                lock (lockObject)
                {
                    detectTask = null;
                }
            }
        }


        /// <summary>
        /// Show some info of the track. There is much more information available
        /// in the Song class but that is not needed here.
        /// </summary>
        private string SongInfo(Song song)
        {
            string songInfo = PerformerList(song.Performers);
            if (songInfo.Length > 0)
            {
                songInfo = song.UniformTitle + " / " + songInfo;
            }
            else
            {
                songInfo = song.UniformTitle;
            }

            songInfo += " [" + song.AlbumTrackID + "/" + song.SongTitle_Link + "]";

            return songInfo;
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


        class DetectSong
        {
            public DateTime StartTime;
            public DateTime EndTime;
            public Song Song;
            public long MSSQL_ID = -1;
            public long MySQL_ID = -1;

            public DetectSong(Song song)
            {
                this.Song = song;
                StartTime = DateTime.Now;
                EndTime = StartTime;
            }
        }

        public static bool Exec_MySQL_RADIOCHANNEL_S(string nameCode, out DataRow row)
        {
            row = null;
            try
            {
                using (MySqlConnection conn = CDR.DB_Helper.NewMySQLConnection())
                {
                    MySqlCommand command = new MySqlCommand();
                    command.CommandText = "RADIOCHANNEL_S";
                    command.Connection = conn;
                    command.CommandType = CommandType.StoredProcedure;
                    command.CommandTimeout = 60; // max 5 minuten voordat we een timeout genereren

                    command.Parameters.Add("@parNAMECODE", MySqlDbType.VarChar, 40).Value = nameCode;

                    MySqlDataAdapter adapter = new MySqlDataAdapter(command);
                    DataSet ds = new DataSet();
                    adapter.Fill(ds);
                    if (ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0)
                    {
                        row = ds.Tables[0].Rows[0];
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

        public static int Exec_MySQL_TITELNUMMERTRACK_ID_COUNT()
        {
            try
            {
                using (MySqlConnection conn = CDR.DB_Helper.NewMySQLConnection())
                {
                    MySqlCommand command = new MySqlCommand();
                    command.Connection = conn;
                    command.CommandType = CommandType.Text;
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
