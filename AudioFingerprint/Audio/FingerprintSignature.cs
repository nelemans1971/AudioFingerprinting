using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AudioFingerprint;

namespace AudioFingerprint.Audio
{
    public class FingerprintSignature : IDisposable
    {
        public int FINGERPRINTSIZE = 256; // for detection a fingerprint of 256 subfingerprints is used

        private static int[] emptyIndexOf = new int[0];

        private long titelnummerTrackID = 0; // Finger ID
        private object reference = null; // identifier
        private object tag = null;

        private byte[] signature = new byte[0];
        private byte[] reliabilities = null; // elke subfinger heeft 32 bytes aan reliabilty info
        private int subFingerprintCount = 0;
        private long durationInMS = -1; // this is the "Real" durarion as calculate from orginal audio file
        private string audioSource = string.Empty;
        private string lokatie = string.Empty;
        private DateTime dateRelease = DateTime.MinValue;
        private string catalogusCode = string.Empty;
        private string uniformeTitleLink = string.Empty;

        private MMHashtable mmhSubFingerLookup = null;
        private Dictionary<uint, int[]> dSubFingerLookup = null;
        private int[] cachedIndexOf = null;
        private uint cachedIndexOfValue = 0;



        public FingerprintSignature()
            : this(null, 0, null, 0)
        {
        }

        public FingerprintSignature(byte[] signature)
            : this(null, 0, signature, 0)
        {
            // we calculate duration based on length signature and knowledge that 1 subfingerprint is 11,6 milliseconds
            durationInMS = (int)(subFingerprintCount * 11.6);
        }

        public FingerprintSignature(byte[] signature, long durationInMS)
            : this(null, 0, signature, durationInMS)
        {
        }

        public FingerprintSignature(object reference, long titelnummerTrackID, byte[] signature, long durationInMS, bool createLookupIndex = false)
        {              
            AudioFingerprint.Math.SimilarityUtility.InitSimilarityUtility();

            this.titelnummerTrackID = titelnummerTrackID;
            this.reference = reference;
            if (this.signature != null)
            {
                Signature = signature;
            }
            this.durationInMS = durationInMS;

            if (createLookupIndex)
            {
                dSubFingerLookup = CreateLookupDictionary();
            }
        }

        public FingerprintSignature(object reference, long titelnummerTrackID, byte[] signature, byte[] mmHashtableData, long durationInMS)
        {
            this.titelnummerTrackID = titelnummerTrackID;
            this.reference = reference;
            if (this.signature != null)
            {
                Signature = signature;
            }
            this.durationInMS = durationInMS;

            if (mmHashtableData != null)
            {
                mmhSubFingerLookup = new MMHashtable(mmHashtableData);
            }
        }

        ~FingerprintSignature()
        {
            Dispose(false);
        }

        /// <summary>
        /// Disposes all used resources and deletes the temporary file.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                dSubFingerLookup = null;
                if (mmhSubFingerLookup != null)
                {
                    mmhSubFingerLookup.Dispose();
                }
                reliabilities = null;
                cachedIndexOf = null;
                cachedIndexOfValue = 0;
            }
        }

        public Dictionary<uint, int[]> CreateLookupDictionary()
        {
            return CreateLookupDictionary(this);
        }

        /// <summary>
        /// Creates a lookup dictionary from the fingerprintsignature. Key is the hash an value is an array of int 
        /// pointing (index) to where the hash can be found using "SubFingerprint(int index)"
        /// </summary>
        public static Dictionary<uint, int[]> CreateLookupDictionary(FingerprintSignature fs, bool filterNoneRandomHashes = true)
        {
            Dictionary<uint, int[]> subFingerLookup = new Dictionary<uint, int[]>(fs.SubFingerprintCount);

            for (int index = 0; index < fs.SubFingerprintCount; index++)
            {
                uint h = BitConverter.ToUInt32(fs.Signature, index * 4);
                if (filterNoneRandomHashes)
                {
                    int bits = AudioFingerprint.Math.SimilarityUtility.HammingDistance(h, 0);
                    if (bits < 10 || bits > 22) // 5 27  (10 22)
                    {
                        // try further in the fingerprint
                        continue;
                    }
                }
                int[] data = new int[1] { index };

                if (subFingerLookup.ContainsKey(h))
                {
                    subFingerLookup[h] = CombineArrays(subFingerLookup[h], data);
                }
                else
                {
                    subFingerLookup.Add(h, data);
                }
            } //foreach

            return subFingerLookup;
        }

        public static MMHashtable CreateMMHashtable(FingerprintSignature fs, bool filterNoneRandomHashes = true)
        {
            Dictionary<uint, int[]> dict = CreateLookupDictionary(fs, filterNoneRandomHashes);
            MMHashtable mh = new MMHashtable(fs.SubFingerprintCount, fs.SubFingerprintCount);
            foreach (KeyValuePair<uint, int[]> entry in dict)
            {
                mh.Add(entry.Key, entry.Value);
            } //foreach
            byte[] byteArray = mh.HashtableAsByteArray;

            return mh;
        }

        
        /// <summary>
        /// Fingerprint ID from lucene/MySQL database
        /// </summary>
        public long FingerTrackID
        {
            get
            {
                return titelnummerTrackID;
            }
            set
            {
                titelnummerTrackID = value;
            }
        }

        /// <summary>
        /// Reference for this Fingerprint signature
        /// </summary>
        public object Reference
        {
            get
            {
                return reference;
            }
            set
            {
                reference = value;
            }
        }

        /// <summary>
        /// User usable storage
        /// </summary>
        public object Tag
        {
            get
            {
                return tag;
            }
            set
            {
                tag = value;
            }
        }
        

        public long DurationInMS
        {
            get
            {
                return durationInMS;
            }
            set
            {
                durationInMS = value;
            }
        }

        public string AudioSource
        {
            get
            {
                return audioSource;
            }
            set
            {
                audioSource = value;
            }
        }

        public string Lokatie
        {
            get
            {
                return lokatie;
            }
            set
            {
                lokatie = value;
            }
        }
        
        public DateTime DateRelease
        {
            get
            {
                return dateRelease;
            }
            set
            {
                dateRelease = value;
            }
        }
        public string CatalogusCode
        {
            get
            {
                return catalogusCode;
            }
            set
            {
                catalogusCode = value;
            }
        }
        
        public string UniformeTitleLink
        {
            get
            {
                return uniformeTitleLink;
            }
            set
            {
                uniformeTitleLink = value;
            }
        }

        /// <summary>
        /// Fingerprint signature for complete audio file
        /// </summary>
        public byte[] Signature
        {
            get
            {
                return signature;
            }
            set
            {
                if ((signature.Length % 4) != 0)
                {
                    throw new Exception("Fingerprint signature must be divideble by 4 and is now " + (signature.Length / 4).ToString());
                }

                signature = value;
                subFingerprintCount = (int)(signature.Length / 4);
            }
        }

        public string SignatureBase64
        {
            get
            {
                return System.Convert.ToBase64String(signature);
            }
        }

        /// <summary>
        /// Every subfinger has 32 bytes reserverd for reliability.
        /// </summary>
        public byte[] Reliabilities
        {
            get
            {
                return reliabilities;
            }
            set
            {
                if ((value.Length % 32) != 0)
                {
                    throw new Exception("Fingerprint reliabilities must be divideble by 32 and is now " + (value.Length / 4).ToString());
                }

                reliabilities = value;
            }
        }

        public string ReliabilitiesBase64
        {
            get
            {
                return System.Convert.ToBase64String(reliabilities);
            }
        }

        public byte[] MMHashtable(bool compress = false)
        {
            if (mmhSubFingerLookup == null)
            {
                mmhSubFingerLookup = CreateMMHashtable(this);
            }

            byte[] byteArray = mmhSubFingerLookup.HashtableAsByteArray;
            if (compress)
            {
                byteArray = Lz4Net.Lz4.CompressBytes(byteArray, 0, byteArray.Length, Lz4Net.Lz4Mode.Fast);
            }

            return byteArray;
        }

        /// <summary>
        /// Return a 32-bit unsigned SubFingerprint
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public uint SubFingerprint(int index)
        {
            if (index >= subFingerprintCount)
            {
                throw new Exception("Index out of bound. index cannot be larger than " + subFingerprintCount.ToString());
            }

            return BitConverter.ToUInt32(signature, index * 4);
        }

        public int SubFingerprintCount
        {
            get
            {
                return subFingerprintCount;
            }
        }

        public byte[] Reliability(int index)
        {
            if (index >= subFingerprintCount || reliabilities == null)
            {
                throw new Exception("Index out of bound. index cannot be larger than " + subFingerprintCount.ToString());
            }

            byte[] r = new byte[32];
            Buffer.BlockCopy(reliabilities, index * 32, r, 0, r.Length);

            return r;
        }

        /// <summary>
        /// Returns an array of 256 consecutive subfingerprints starting at "indexOfSubFingerprint"
        /// or null when 256 subfingerprint are not possible
        /// </summary>
        public uint[] Fingerprint(int indexOfSubFingerprint)
        {
            if ((indexOfSubFingerprint + 255) >= subFingerprintCount)
            {
                return null;
            }

            uint[] fingerprint = new uint[256];
            for (int i = 0; i < 256; i++)
            {
                fingerprint[i] = BitConverter.ToUInt32(signature, i + (indexOfSubFingerprint * 4));
            } //for i


            return fingerprint;
        }

        public int FingerprintCount
        {
            get
            {
                return (int)(subFingerprintCount / 256);
            }
        }
        
        /// <summary>
        /// Return a array of index values where "value" occures as subfingerprint in the fingerprintsignature
        /// </summary>
        public int[] IndexOf(uint value)
        {
            if (cachedIndexOfValue == value && cachedIndexOf != null)
            {
                return cachedIndexOf;
            }

            if (dSubFingerLookup == null && mmhSubFingerLookup == null)
            {
                dSubFingerLookup = CreateLookupDictionary();
            }

            if (mmhSubFingerLookup != null)
            {
                int[] indexOf;
                if (mmhSubFingerLookup.TryKeyValue(value, out indexOf))
                {
                    cachedIndexOfValue = value;
                    cachedIndexOf = indexOf;
                    return cachedIndexOf;
                }
            }
            else
            {
                int[] indexOf;
                if (dSubFingerLookup.TryGetValue(value, out indexOf))
                {
                    cachedIndexOfValue = value;
                    cachedIndexOf = indexOf;
                    return cachedIndexOf;
                }
            }

            return emptyIndexOf;
        }

        /// <summary>
        /// Slow methode
        /// 
        /// Return a array of index values where "value" occures as subfingerprint in the fingerprintsignature
        /// </summary>
        public int[] IndexOfSlow(uint value)
        {
            if (cachedIndexOfValue == value && cachedIndexOf != null)
            {
                return cachedIndexOf;
            }

            // Slow methode
            List<int> indexesOf = new List<int>();

            uint[] subfingerprints = new uint[subFingerprintCount];
            for (int i = 0; i < signature.Length; i += 4)
            {
                subfingerprints[i / 4] = BitConverter.ToUInt32(signature, i);
            } //for


            int startIndex = 0;
            while (startIndex < (subFingerprintCount - 256))
            {
                int index = Array.IndexOf(subfingerprints, value, startIndex);
                if (index < 0)
                {
                    // we zijn klaar
                    break;
                }

                startIndex = index + 1;
                if (index >= 0 && subFingerprintCount > (index + 256))
                {
                    indexesOf.Add(index);
                    if (indexesOf.Count > 256)
                    {
                        // too many hits is not good!
                        break;
                    }
                }
            } //while


            // cache calculated result
            cachedIndexOfValue = value;
            cachedIndexOf = indexesOf.ToArray();
            return cachedIndexOf;
        }

        /// <summary>
        /// Calculate hamming distance of bits for two fingerprints (normal a fingerprint contains 256 subfingerprints)
        /// The return value is the Bit Error Rate.
        /// For 256 subfingerprints a BER lower than 2867 is considered a match
        /// </summary>
        public static int HammingDistance(uint[] fingerprint1, uint[] fingerprint2)
        {
            if (fingerprint1 == null || fingerprint2 == null)
            {
                throw new Exception("fingerprint1 or fingerprint2 is null");
            }
            else if (fingerprint1.Length != fingerprint2.Length)
            {
                throw new Exception(string.Format("fingerprint1 and fingerprint2 must be of equal length ({0} != {1})", fingerprint1.Length, fingerprint2.Length));
            }

            // length of both needs to be equal!
            int BER = 0; // Bit Error Rate
            for (int i = 0; i < fingerprint1.Length; i++)
            {
                int bits = AudioFingerprint.Math.SimilarityUtility.HammingDistance(fingerprint1[i], fingerprint2[i]);
                if (bits > 0)
                {
                    BER += bits;
                }
            } // for i

            return BER;
        }

        /// <summary>
        /// Return Bit Error Rate for which a value lower than this can be consired a match.
        /// 0.35 is the factor
        /// </summary>
        public static int BER(int subFingerprintCount = 256)
        {
            // 8192 bits 0.35 = ber lower than 2867 
            return (int)((subFingerprintCount * 32) * 0.35);
        }

        /// <summary>
        /// Calculate the BER between 2 fingerprints
        /// </summary>
        public static int BER(FingerprintSignature f1, FingerprintSignature f2)
        {
            if (f1.SubFingerprintCount != f2.SubFingerprintCount)
            {
                return -1;
            }

            // length of both needs to be equal!
            int BER = 0; // Bit Error Rate
            for (int i = 0; i < f1.SubFingerprintCount; i++)
            {
                int bits = AudioFingerprint.Math.SimilarityUtility.HammingDistance(f1.SubFingerprint(i), f2.SubFingerprint(i));
                if (bits > 0)
                {
                    BER += bits;
                }
            } // for i

            return BER;
        }


        public void SaveHashAsText(string filename)
        {
            using (System.IO.StreamWriter writer = new System.IO.StreamWriter(System.IO.File.Open(System.IO.Path.GetFileNameWithoutExtension(filename) + ".hash", System.IO.FileMode.Create)))
            {
                for (int i = 0; i < this.SubFingerprintCount; i++)
                {
                    uint h = this.SubFingerprint(i);
                    writer.WriteLine(h.ToString());
                } //foreach

                writer.WriteLine("---");

                for (int i = 0; i < this.SubFingerprintCount; i++)
                {
                    byte[] r = this.Reliability(i); // 32 byte values per hash
                    foreach (byte b in r)
                    {
                        writer.WriteLine(b.ToString());
                    }
                } //foreach
            }
        }

        public static FingerprintSignature ReadHashFromText(string filename)
        {
            filename = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(filename), System.IO.Path.GetFileNameWithoutExtension(filename));

            List<uint> data = new List<uint>();
            List<byte[]> r = new List<byte[]>();
            using (System.IO.StreamReader reader = new System.IO.StreamReader(System.IO.File.Open(filename + ".hash", System.IO.FileMode.Open)))
            {
                bool doHash = true;
                long countR = 0;
                byte[] tmpR = new byte[32];

                while (!reader.EndOfStream)
                {
                    string line = reader.ReadLine();
                    if (line.Length > 0)
                    {
                        if (line == "---")
                        {
                            doHash = false;
                            continue; // op naar volgende regel voor reliabilty
                        }

                        if (doHash)
                        {
                            data.Add(Convert.ToUInt32(line));
                        }
                        else
                        {
                            if (countR >= 32)
                            {
                                r.Add(tmpR);
                                tmpR = new byte[32];
                                countR = 0;
                            }

                            tmpR[countR] = Convert.ToByte(line);
                            countR++;
                        }
                    }
                } //while

                if (countR > 0)
                {
                    r.Add(tmpR);
                    tmpR = null;
                    countR = 0;
                }

            } //using

            byte[] signature = new byte[data.Count * sizeof(uint)];
            for (int i = 0; i < data.Count; i++)
            {
                byte[] b = BitConverter.GetBytes(data[i]);
                Buffer.BlockCopy(b, 0, signature, i * sizeof(uint), b.Length);
            } //for i
                        
            byte[] reliabilities = new byte[r.Count * 32];
            for (int i = 0; i < r.Count; i++)
            {
                Buffer.BlockCopy(r[i], 0, reliabilities, i * 32, r[i].Length);
            } //for i

            FingerprintSignature fs = new FingerprintSignature(System.IO.Path.GetFileNameWithoutExtension(filename), 0, signature, (long)(signature.Length * 11.6));
            fs.reliabilities = reliabilities;

            return fs;
        }


        private static int[] CombineArrays(params int[][] arrays)
        {
            int[] rv = new int[arrays.Sum(a => a.Length)];
            int offset = 0;
            foreach (int[] array in arrays)
            {
                System.Buffer.BlockCopy(array, 0, rv, offset, array.Length * sizeof(int));
                offset += array.Length * sizeof(int);
            }
            return rv;
        }
    }
}
