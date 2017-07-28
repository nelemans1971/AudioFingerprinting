namespace AudioFingerprint.Math
{
    public class SimilarityUtility
    {
        private static ushort[][] hammingDistanceTable = null;

        public static void InitSimilarityUtility()
        {
            Setup_HammingDistance();
        }

        private static void Setup_HammingDistance()
        {
            if (hammingDistanceTable == null)
            {
                ushort[][] tmpHammingDistanceTable = new ushort[256][];
                for (int i = 0; i < 256; i++)
                {
                    tmpHammingDistanceTable[i] = new ushort[256];
                }


                for (uint j = 0; j < 256; j++)
                {
                    for (uint i = 0; i < 256; i++)
                    {
                        tmpHammingDistanceTable[i][j] = PreCalc_HammingDistance(i, j);
                    }
                }

                // Import so global var is set when it is filled!
                hammingDistanceTable = tmpHammingDistanceTable;
            }
        }

        private static ushort PreCalc_HammingDistance(uint fp1, uint fp2)
        {
            ushort result = 0;
            for (byte i = 0; i <= 32; i++)
            {
                if ((fp1 & 1) != (fp2 & 1))
                {
                    result++;
                }
                fp1 >>= 1;
                fp2 >>= 1;
            }

            return result;
        }

        public static ushort HammingDistance(uint fp1, uint fp2)
        {
            ushort result = 0;

            /// perform 4 8bit precalc table lookups
            for (byte i = 0; i < 4; i++)
            {
                result += hammingDistanceTable[fp1 & 255][fp2 & 255];
                fp1 >>= 8;
                fp2 >>= 8;
            }

            return result;
        }

    }
}
