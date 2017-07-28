namespace AcoustID
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public class FingerprintAcoustID
    {
        public const int AcoustIDStartPos = 80; // ongeveer 10 seconden in de muziek
        public const int AcoustIDFingeprintLen = 360; // was 120


        private static int[] emptyIndexOf = new int[0];

        private object reference = null; // identifier
        private object tag = null;

        private byte[] signature = new byte[0];
        private long durationInMS = -1; // this is the "Real" durarion as calculate from orginal audio file
        private string audioSource = string.Empty;
        private string lokatie = string.Empty;        
        private DateTime dateRelease = DateTime.MinValue;
        private string catalogusCode = string.Empty;
        private string uniformeTitleLink = string.Empty;

        private Dictionary<int, int[]> dSubFingerLookup = null;


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
        public int[] SignatureInt32
        {
            get
            {
                int[] result = new int[(int)(signature .Length / sizeof(int))];
                Buffer.BlockCopy(signature, 0, result, 0, signature.Length);
                return result;
            }
            set
            {                
                signature = new byte[value.Length * sizeof(int)];
                Buffer.BlockCopy(value, 0, signature, 0, signature.Length);
            }
        }

        public byte[] Signature
        {
            get
            {
                byte[] result = new byte[signature.Length];
                Buffer.BlockCopy(signature, 0, result, 0, signature.Length);
                return result;
            }
            set
            {
                signature = new byte[value.Length];
                Buffer.BlockCopy(value, 0, signature, 0, signature.Length);
            }
        }

        public int[] AcoustID_Extract_Query
        {
            get
            {
                int[] subFingers = SignatureInt32;
                List<int> query = new List<int>();

                int cleansize = 0;
                for (int i = 0; i < subFingers.Length; i++)
                {
                    if (subFingers[i] != 627964279 && subFingers[i] != 0)
                    {
                        cleansize++;
                    }
                }

                if (cleansize <= 0)
                {
                    return new int[0];
                }

                for (int i = Math.Max(0, Math.Min(cleansize - AcoustIDFingeprintLen, AcoustIDStartPos)); i < subFingers.Length && query.Count < AcoustIDFingeprintLen; i++)
                {
                    if (subFingers[i] == 627964279 || subFingers[i] == 0)
                    {
                        // silence (skip it)
                        continue;
                    }

                    int subFingerValue = subFingers[i] & unchecked((int)0xfffffff0); // orgineel is het "unchecked((int)0xfffffff0)" maar levert niet alles op 

                    // deduplicate
                    if (query.Contains(subFingerValue))
                    {
                        continue;
                    }
                    query.Add(subFingerValue);
                } //for

                return query.ToArray();
            }
        }

        /// <summary>
        /// Return a array of index values where "value" occures as subfingerprint in the fingerprintsignature
        /// </summary>
        public int[] IndexOf(int value)
        {
            if (dSubFingerLookup == null)
            {
                dSubFingerLookup = CreateLookupDictionary(this);
            }

            int[] indexOf;
            if (dSubFingerLookup.TryGetValue(value, out indexOf))
            {
                return indexOf;
            }

            return emptyIndexOf;
        }

        /// <summary>
        /// Creates a lookup dictionary from the fingerprintsignature. Key is the hash an value is an array of int 
        /// pointing (index) to where the hash can be found using "SubFingerprint(int index)"
        /// </summary>
        public static Dictionary<int, int[]> CreateLookupDictionary(FingerprintAcoustID fs)
        {
            int[] hashes = fs.SignatureInt32;

            Dictionary<int, int[]> subFingerLookup = new Dictionary<int, int[]>(hashes.Length);

            for (int index = 0; index < hashes.Length; index++)
            {
                int h = hashes[index] & unchecked((int)0xfffffff0);

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
        
        public static int HammingDistance(int[] fingerprint1, int[] fingerprint2)
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
                int bits = AudioFingerprint.Math.SimilarityUtility.HammingDistance(unchecked((uint)fingerprint1[i]), unchecked((uint)fingerprint2[i]));
                if (bits > 0)
                {
                    BER += bits;
                }
            } // for i

            return BER;
        }


        private const int MATCH_BITS = 14;
        private const int MATCH_MASK = ((1 << MATCH_BITS) - 1); // (2 ^ 14) -1 
        private const int UNIQ_MASK = ((1 << MATCH_BITS) - 1);

        private static ushort MatchStrip(int x)
        {
            return (ushort)(unchecked((uint)x) >> (32 - MATCH_BITS));
        }

        private static int UniqueStrip(int x)
        {
            return (int)(unchecked((uint)x) >> (32 - MATCH_BITS));
        }

        public static float MatchFingerprint(FingerprintAcoustID finger1, FingerprintAcoustID finger2, int maxoffset = 0)
        {
            float score = 0.0f;
            int[] f1 = finger1.SignatureInt32;
            int f1size = f1.Length;
            int[] f2 = finger2.SignatureInt32;
            int f2size = f2.Length;
            int numcounts = f1size + f2size + 1;

            ushort[] aoffsets = new ushort[MATCH_MASK + 1];
            ushort[] boffsets = new ushort[MATCH_MASK + 1];

            // Zorg dat er een limiet is
            if (f1size > aoffsets.Length)
            {
                f1size = aoffsets.Length;
            }
            if (f2size > boffsets.Length)
            {
                f2size = boffsets.Length;
            }

            // ------------------------------------------------------------
            // YN aanpassing 2015-11-02 gedaan (zoeken duurt anders te lang
            // zie titelnummer_id=944086
            // Lookup tabel aanmaken ????
            for (ushort i = 0; i < f1size; i++)
            {
                aoffsets[MatchStrip(f1[i])] = i;
            }
            for (ushort i = 0; i < f2size; i++)
            {
                boffsets[MatchStrip(f2[i])] = i;
            }
            // ------------------------------------------------------------


            int topcount = 0;
            int topoffset = 0;
            ushort[] counts = new ushort[numcounts];
            for (int i = 0; i < MATCH_MASK; i++)
            {
                if (aoffsets[i] != 0 && boffsets[i] != 0)
                {
                    int offset = aoffsets[i] - boffsets[i];
                    if (maxoffset == 0 || (-maxoffset <= offset && offset <= maxoffset))
                    {
                        offset += f2size;
                        counts[offset]++;
                        if (counts[offset] > topcount)
                        {
                            topcount = counts[offset];
                            topoffset = offset;
                        }
                    }
                }
            } //for i

            topoffset -= f2size;

            int f1Offset = 0;
            int f2Offset = 0;
            int minsize = Math.Min(f1size, f2size) & ~1;
            if (topoffset < 0)
            {
                f2Offset -= topoffset;
                f2size = Math.Max(0, f2size + topoffset);
            }
            else
            {
                f1Offset += topoffset;
                f1size = Math.Max(0, f1size - topoffset);
            }

            int size = Math.Min(f1size, f2size) / 2;
            if (size == 0 || minsize == 0)
            {
                System.Diagnostics.Trace.WriteLine("acoustid_compare2: empty matching subfingerprint");
                return 0.0f;
            }


            ushort[] seen = aoffsets;
            
            int f1unique = 0;
            // clear
            for (int i = 0; i < UNIQ_MASK; i++)
            {
                seen[i] = 0;
            }
            for (int i = 0; i < f1size; i++)
            {
                int key = UniqueStrip(f1[i]);
                if (seen[key] == 0)
                {
                    f1unique++;
                    seen[key] = 1;
                }
            }

            int f2unique = 0;
            // clear
            for (int i = 0; i < UNIQ_MASK; i++)
            {
                seen[i] = 0;
            }
            for (int i = 0; i < f2size; i++)
            {
                int key = UniqueStrip(f2[i]);
                if (seen[key] == 0)
                {
                    f2unique++;
                    seen[key] = 1;
                }
            }

            float diversity = (float)Math.Min(Math.Min(1.0f, (float)(f1unique + 10) / f1size + 0.5f), Math.Min(1.0, (float)(f2unique + 10) / f2size + 0.5f));


            int BER = 0;
            for (int i = 0; i < size; i++)
            {
                int bits = AudioFingerprint.Math.SimilarityUtility.HammingDistance(unchecked((uint)f1[i]), unchecked((uint)f2[i]));
                if (bits > 0)
                {
                    BER += bits;
                }
            } //for i
            score = (size * 2.0f / minsize) * (1.0f - 2.0f * (float)BER / (64 * size));
            if (score < 0.0f)
            {
                score = 0.0f;
            }

            if (diversity < 1.0)
            {
                float newscore = (float)Math.Pow(score, 8.0f - 7.0f * diversity);
                System.Diagnostics.Trace.WriteLine(string.Format("acoustid_compare2: scaling score because of duplicate items, {0:#0:00} => {1:#0:00}", score, newscore));
                score = newscore;
            }

            return score;
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
