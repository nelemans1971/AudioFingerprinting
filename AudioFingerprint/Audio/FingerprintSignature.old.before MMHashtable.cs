using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AudioFingerprint.Audio
{
    public class FingerprintSignature
    {
        private object reference = null; // identifier
        private object tag = null;

        private byte[] signature = new byte[0];
        private int subFingerprintCount = 0;
        private long durationInMS = -1; // this is the "Real" durarion as calculate from orginal audio file
        private string audioSource = "";

        private Dictionary<uint, int[]> subFingerLookup = null;
        private int[] cachedIndexOf = null;
        private uint cachedIndexOfValue = 0;


        public byte[] reliabilities = null; // elke subfinger heeft 32 bytes aan reliabilty info

        public FingerprintSignature()
            : this(null, null, 0)
        {
        }

        public FingerprintSignature(byte[] signature)
            : this(null, signature, 0)
        {
            // we calculate duration based on length signature and knowledge that 1 subfingerprint is 11,6 milliseconds
            durationInMS = (int)(subFingerprintCount * 11.6);
        }

        public FingerprintSignature(byte[] signature, long durationInMS)
            : this(null, signature, durationInMS)
        {
        }

        public FingerprintSignature(object reference, byte[] signature, long durationInMS, bool createLookupIndex = false)
        {
            this.reference = reference;
            if (this.signature != null)
            {
                Signature = signature;
            }
            this.durationInMS = durationInMS;

            if (createLookupIndex)
            {
                subFingerLookup = CreateLookupDictionary(this);
            }
        }

        public Dictionary<uint, int[]> CreateLookupDictionary()
        {
            return CreateLookupDictionary(this);
        }

        /// <summary>
        /// Creates a lookup dictionary from the fingatprintsignature. Key is the hash an value is an array of int 
        /// pointing (index) to where the hash can be found using "SubFingerprint(int index)"
        /// </summary>
        public static Dictionary<uint, int[]> CreateLookupDictionary(FingerprintSignature fs)
        {
            Dictionary<uint, int[]> subFingerLookup = new Dictionary<uint, int[]>(fs.SubFingerprintCount);

            for (int index = 0; index < fs.SubFingerprintCount; index++)
            {
                uint h = BitConverter.ToUInt32(fs.Signature, index * 4);
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

        /// <summary>
        /// Return a 32-bit unsigned SubFingerprint
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public uint SubFingerprint(int index)
        {
            if (index >= subFingerprintCount)
            {
                throw new Exception("Index out of bound. index cannot we larger than " + subFingerprintCount.ToString());
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
            if (index >= subFingerprintCount && reliabilities != null)
            {
                throw new Exception("Index out of bound. index cannot we larger than " + subFingerprintCount.ToString());
            }

            byte[] r = new byte[32];
            Buffer.BlockCopy(reliabilities, index * 32, r, 0, 32);
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

            if (subFingerLookup != null)
            {
                if (subFingerLookup.ContainsKey(value))
                {
                    cachedIndexOfValue = value;
                    cachedIndexOf = subFingerLookup[value];
                    return cachedIndexOf;
                }
                return new int[0];
            }

            List<int> indexesOf = new List<int>();

            // Slow methode
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
            }
        }

        public static FingerprintSignature ReadHashFromText(string filename)
        {
            List<uint> data = new List<uint>();
            using (System.IO.StreamReader reader = new System.IO.StreamReader(System.IO.File.Open(System.IO.Path.GetFileNameWithoutExtension(filename) + ".hash", System.IO.FileMode.Open)))
            {
                while (!reader.EndOfStream)
                {
                    string line = reader.ReadLine();
                    if (line.Length > 0)
                    {
                        data.Add(Convert.ToUInt32(line));
                    }
                } //while
            } //using

            byte[] signature = new byte[data.Count * sizeof(uint)];
            for (int i = 0; i < data.Count; i++)
            {
                byte[] b = BitConverter.GetBytes(data[i]);
                Buffer.BlockCopy(b, 0, signature, i * sizeof(uint), b.Length);

            } //for i

            return new FingerprintSignature(System.IO.Path.GetFileNameWithoutExtension(filename), signature, (long)(signature.Length * 11.6));
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
