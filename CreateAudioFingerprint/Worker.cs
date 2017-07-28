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
using AudioFingerprint;
using AudioFingerprint.Audio;
using AcoustID;
using AcoustID.Audio;
using System.IO;
using MySql.Data.MySqlClient;
using System.Reflection;

namespace CreateAudioFingerprint
{
    class Worker
    {
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

            AudioFingerprint.Audio.BassLifetimeManager.bass_EMail = ini.IniReadValue("BASS", "bass_EMail", "");
            AudioFingerprint.Audio.BassLifetimeManager.bass_RegistrationKey = ini.IniReadValue("BASS", "bass_RegistrationKey", "");


            AudioFingerprint.Math.SimilarityUtility.InitSimilarityUtility();
        }

        public void Run(string wildcard)
        {
            ScanDirectoryAndFingerprint(wildcard);
        }

        public void ScanDirectoryAndFingerprint(string directory)
        {
            // Expand directory to full directory path.
            // We needed to remove filename part because GetFullPath doesn't suppport wildcards
            string wildcard = Path.GetFileName(directory);
            directory = Path.GetDirectoryName(directory);
            directory = Path.GetFullPath(directory);
            directory = Path.Combine(directory, wildcard);

            foreach (string f in Directory.GetFiles(Path.GetDirectoryName(directory), Path.GetFileName(directory)))
            {
                Console.WriteLine(Path.GetFileName(f));
                
                // For best result you should return the same key for the same file. Muziekweb.nl uses an cataloguenumber (some chars and a number, 7 or 8 char long in total) 
                // then we added the track number. (Fieldname: TITELNUMMERTRACK)
                string referenceKey = GenerateCRC32Key(f);


                FingerprintAcoustID fingerprint = MakeAcoustIDFinger(referenceKey, f);
                FingerprintSignature subFingerprint = MakeSubFingerID(referenceKey, f);

                // Fill with some subdata
                if (subFingerprint != null && fingerprint != null)
                {
                    // AcoustID
                    fingerprint.AudioSource = Path.GetExtension(f).Replace(".", "").ToUpper();
                    fingerprint.Lokatie = f; // Path.GetDirectoryName(f);
                    fingerprint.DateRelease = DateTime.Now.Date;
                    fingerprint.CatalogusCode = "POPULAIR"; // POPULAIR or CLASSICAL
                    fingerprint.UniformeTitleLink = ""; // used in muziekweb.nl database to find the same track on different albums
                                                        // Subfinger
                    subFingerprint.AudioSource = Path.GetExtension(f).Replace(".", "").ToUpper();
                    subFingerprint.Lokatie = f;// Path.GetDirectoryName(f);
                    subFingerprint.DateRelease = DateTime.Now.Date;
                    subFingerprint.CatalogusCode = "POPULAIR"; // POPULAIR or CLASSICAL
                    subFingerprint.UniformeTitleLink = ""; // used in muziekweb.nl database to find the same track on different albums
                }

                // Store the data in de MySQL Database, this will later be used to create an inverted index using lucene.
                int titelnummertrackID;
                Exec_MySQL_SUBFINGERID_IU(subFingerprint.Reference.ToString(), subFingerprint.CatalogusCode, subFingerprint.DateRelease, 
                    subFingerprint.UniformeTitleLink, subFingerprint.AudioSource, subFingerprint.Lokatie, subFingerprint.DurationInMS, subFingerprint.Signature, out titelnummertrackID);
                Exec_MySQL_FINGERID_IU(fingerprint.Reference.ToString(), fingerprint.CatalogusCode, fingerprint.DateRelease,
                    fingerprint.UniformeTitleLink, fingerprint.AudioSource, fingerprint.Lokatie, fingerprint.DurationInMS, fingerprint.Signature, out titelnummertrackID);
            } //foreach
        }

        /// <summary>
        /// Substitue for CatalogeTrack number in Muziekweb.nl database
        /// </summary>
        public string GenerateCRC32Key(string input)
        {
            // step 1, calculate MD5 hash from input
            DamienG.Security.Cryptography.Crc32 crc32 = new DamienG.Security.Cryptography.Crc32();
            byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(input);
            byte[] hash = crc32.ComputeHash(inputBytes);

            // step 2, convert byte array to hex string
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < hash.Length; i++)
            {
                sb.Append(hash[i].ToString("X2"));
            } //for

            return sb.ToString();
        }


        private string GenerateKey()
        {
            Guid g = Guid.NewGuid();
            string GuidString = Convert.ToBase64String(g.ToByteArray());
            GuidString = GuidString.Replace("=", "");
            GuidString = GuidString.Replace("+", "");

            return GuidString;
        }

        #region Fingerprint creation

        private FingerprintSignature MakeSubFingerID(string key, string filename)
        {
            FingerprintSignature fingerprint = null;

            AudioEngine audioEngine = new AudioEngine();
            try
            {
                SpectrogramConfig spectrogramConfig = new DefaultSpectrogramConfig();

                AudioSamples samples = null;
                try
                {
                    // First read audio file and downsample it to mono 5512hz
                    samples = audioEngine.ReadMonoFromFile(filename, spectrogramConfig.SampleRate, 0, -1);
                }
                catch
                {
                    return null;
                }

                // No slice the audio is chunks seperated by 11,6 ms (5512hz 11,6ms = 64 samples!)
                // An with length of 371ms (5512kHz 371ms = 2048 samples [rounded])
                fingerprint = audioEngine.CreateFingerprint(samples, spectrogramConfig);
                if (fingerprint != null)
                {
                    fingerprint.Reference = key;
                }
            }
            finally
            {
                if (audioEngine != null)
                {
                    audioEngine.Close();
                    audioEngine = null;
                }
            }

            return fingerprint;
        }

        private AcoustID.FingerprintAcoustID MakeAcoustIDFinger(string key, string filename)
        {
            // resample to 11025Hz
            IAudioDecoder decoder = new BassDecoder();
            try
            {
                decoder.Load(filename);

                ChromaContext context = new ChromaContext();

                context.Start(decoder.SampleRate, decoder.Channels);
                decoder.Decode(context.Consumer, 120);
                if (context.Finish())
                {
                    FingerprintAcoustID fingerprint = new FingerprintAcoustID();
                    fingerprint.Reference = key;
                    fingerprint.DurationInMS = (long)decoder.Duration * 1000;
                    fingerprint.SignatureInt32 = context.GetRawFingerprint();

                    return fingerprint;
                }
            }
            catch (Exception e)
            {
                // Probleem waarschijnlijk met file.
                Console.Error.WriteLine(e.ToString());
            }
            finally
            {
                decoder.Dispose();
            }

            return null;
        }

        #endregion


        #region MySQL

        // Needed when bulk inserting data (+7 milion tracks) from different computers
        // We got deadlocks and deal with it using random timeouts.
        private static Random random = new Random();

        public static bool Exec_MySQL_SUBFINGERID_IU(string titelnummertrack, string catalogusCode, DateTime datumRelease, string uniformetitelLink, string audioSource, string lokatie, long durationInMS, byte[] signature, out int titelnummertrackID)
        {
            titelnummertrackID = -1;

            Exec_MySQL_SUBFINGERID_D(titelnummertrack);

            // nu zorgen dat computer naam in de database komt en wij deze als ID
            // in deze class opslaan
            bool deadlocked = true;
            while (deadlocked)
            {
                deadlocked = false;
                try
                {
                    using (MySqlConnection conn = CDR.DB_Helper.NewMySQLConnection())
                    {
                        MySqlCommand command = new MySqlCommand();
                        command.Connection = conn;
                        command.CommandText = "SUBFINGERID_IU";
                        command.CommandType = CommandType.StoredProcedure;
                        command.CommandTimeout = 60;

                        if (lokatie.Length > 256)
                        {
                            lokatie = lokatie.Substring(0, 256);
                        }

                        command.Parameters.Add("@parTITELNUMMERTRACK", MySqlDbType.VarChar, 20).Value = titelnummertrack;
                        command.Parameters.Add("@parCATALOGUSCODE", MySqlDbType.VarChar, 12).Value = catalogusCode;
                        command.Parameters.Add("@parDATUMRELEASE", MySqlDbType.DateTime, 0).Value = datumRelease;
                        command.Parameters.Add("@parUNIFORMETITELLINK", MySqlDbType.VarChar, 12).Value = uniformetitelLink.ToUpper();
                        command.Parameters.Add("@parAUDIOSOURCE", MySqlDbType.VarChar, 12).Value = audioSource.ToUpper();
                        command.Parameters.Add("@parLOKATIE", MySqlDbType.VarChar, 256).Value = lokatie.ToUpper();
                        command.Parameters.Add("@parDURATIONINMS", MySqlDbType.Int64, 0).Value = durationInMS;
                        command.Parameters.Add("@parSIGNATURE", MySqlDbType.Blob, 0).Value = signature;

                        titelnummertrackID = Convert.ToInt32(command.ExecuteScalar());
                        return true;
                    }
                }
                catch (Exception e)
                {
                    if (e.ToString().Contains("deadlocked"))
                    {
                        System.Threading.Thread.Sleep(random.Next(50, 500));
                        deadlocked = true;
                    }
                    else if (e.ToString().Contains("duplicate key row"))
                    {
                        // ignore
                        Console.WriteLine(titelnummertrack + " | duplicate key");
                        return true;
                    }
                    else
                    {
                        Console.Error.WriteLine(e.ToString());
                    }
                }
            } //while

            return false;
        }

        public static bool Exec_MySQL_SUBFINGERID_D(string titelnummertrack)
        {
            bool deadlocked = true;
            while (deadlocked)
            {
                try
                {
                    using (MySqlConnection conn = CDR.DB_Helper.NewMySQLConnection())
                    {
                        MySqlCommand command = new MySqlCommand();
                        command.Connection = conn;
                        command.CommandText = "SUBFINGERID_D";
                        command.CommandType = CommandType.StoredProcedure;
                        command.CommandTimeout = 60;

                        command.Parameters.Add("@parTITELNUMMERTRACK", MySqlDbType.VarChar, 20).Value = titelnummertrack;
                        command.ExecuteNonQuery();
                        return true;
                    }
                }
                catch (Exception e)
                {
                    if (e.ToString().Contains("deadlocked"))
                    {
                        System.Threading.Thread.Sleep(random.Next(50, 500));
                        deadlocked = true;
                    }
                    else
                    {
                        Console.Error.WriteLine(e.ToString());
                    }
                }
            } //while

            return false;
        }

        public static bool Exec_MySQL_FINGERID_IU(string titelnummertrack, string catalogusCode, DateTime datumRelease, string uniformetitelLink, string audioSource, string lokatie, long durationInMS, byte[] signature, out int titelnummertrackID)
        {
            titelnummertrackID = -1;

            Exec_MySQL_FINGERID_D(titelnummertrack);

            // nu zorgen dat computer naam in de database komt en wij deze als ID
            // in deze class opslaan
            bool deadlocked = true;
            while (deadlocked)
            {
                deadlocked = false;
                try
                {
                    using (MySqlConnection conn = CDR.DB_Helper.NewMySQLConnection())
                    {
                        MySqlCommand command = new MySqlCommand();
                        command.Connection = conn;
                        command.CommandText = "FINGERID_IU";
                        command.CommandType = CommandType.StoredProcedure;
                        command.CommandTimeout = 60;

                        command.Parameters.Add("@parTITELNUMMERTRACK", MySqlDbType.VarChar, 20).Value = titelnummertrack;
                        command.Parameters.Add("@parCATALOGUSCODE", MySqlDbType.VarChar, 12).Value = catalogusCode;
                        command.Parameters.Add("@parDATUMRELEASE", MySqlDbType.DateTime, 0).Value = datumRelease;
                        command.Parameters.Add("@parUNIFORMETITELLINK", MySqlDbType.VarChar, 12).Value = uniformetitelLink.ToUpper();
                        command.Parameters.Add("@parAUDIOSOURCE", MySqlDbType.VarChar, 12).Value = audioSource.ToUpper();
                        command.Parameters.Add("@parLOKATIE", MySqlDbType.VarChar, 40).Value = lokatie.ToUpper();
                        command.Parameters.Add("@parDURATIONINMS", MySqlDbType.Int64, 0).Value = durationInMS;
                        command.Parameters.Add("@parSIGNATURE", MySqlDbType.Blob, 0).Value = signature;

                        titelnummertrackID = Convert.ToInt32(command.ExecuteScalar());
                        return true;
                    }
                }
                catch (Exception e)
                {
                    if (e.ToString().Contains("deadlocked"))
                    {
                        System.Threading.Thread.Sleep(random.Next(50, 500));
                        deadlocked = true;
                    }
                    else if (e.ToString().Contains("duplicate key row"))
                    {
                        // ignore
                        Console.Error.WriteLine(titelnummertrack + " | duplicate key");
                        return true;
                    }
                    else
                    {
                        Console.Error.WriteLine(e.ToString());
                    }
                }
            } //while

            return false;
        }

        public static bool Exec_MySQL_FINGERID_D(string titelnummertrack)
        {
            bool deadlocked = true;
            while (deadlocked)
            {
                try
                {
                    using (MySqlConnection conn = CDR.DB_Helper.NewMySQLConnection())
                    {
                        MySqlCommand command = new MySqlCommand();
                        command.Connection = conn;
                        command.CommandText = "FINGERID_D";
                        command.CommandType = CommandType.StoredProcedure;
                        command.CommandTimeout = 60;

                        command.Parameters.Add("@parTITELNUMMERTRACK", MySqlDbType.VarChar, 20).Value = titelnummertrack;
                        command.ExecuteNonQuery();
                        return true;
                    }
                }
                catch (Exception e)
                {
                    if (e.ToString().Contains("deadlocked"))
                    {
                        System.Threading.Thread.Sleep(random.Next(50, 500));
                        deadlocked = true;
                    }
                    else
                    {
                        Console.Error.WriteLine(e.ToString());
                    }
                }
            } //while

            return false;
        }

        #endregion

    }
}
