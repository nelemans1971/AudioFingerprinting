// -----------------------------------------------------------------------
// <copyright file="FingerprintDecompressor.cs" company="">
// Original C++ implementation by Lukas Lalinsky, http://acoustid.org/chromaprint
// </copyright>
// -----------------------------------------------------------------------

namespace AcoustID.Chromaprint
{
    using System.Collections.Generic;
    using AcoustID.Util;

    /// <summary>
    /// TODO: Update summary.
    /// </summary>
    public class FingerprintDecompressor
    {
        static readonly int kMaxNormalValue = 7;
        static readonly int kNormalBits = 3;
        static readonly int kExceptionBits = 5;

        List<int> m_result;
        List<byte> m_bits = new List<byte>();

        public int[] Decompress(string data, ref int algorithm)
        {
            if (data.Length < 4)
            {
                // Invalid fingerprint (shorter than 4 bytes)
                return new int[0];
            }

            // TODO: this is not exactly what the C++ version does
            if (algorithm <= 0)
            {
                algorithm = (int)data[0];
            }

            int length = ((byte)(data[1]) << 16) | ((byte)(data[2]) << 8) | ((byte)(data[3]));

            BitStringReader reader = new BitStringReader(data);
            reader.Read(8);
            reader.Read(8);
            reader.Read(8);
            reader.Read(8);

            if (reader.AvailableBits() < length * kNormalBits)
            {
                // Invalid fingerprint (too short)
                return new int[0];
            }

            m_result = new List<int>(length);

            for (int i = 0; i < length; i++)
            {
                m_result.Add(-1);
            }

            reader.Reset();
            if (!ReadNormalBits(reader))
            {
                return new int[0];
            }

            reader.Reset();
            if (!ReadExceptionBits(reader))
            {
                return new int[0];
            }

            UnpackBits();

            // TODO: no list needed?
            return m_result.ToArray();
        }

        void UnpackBits()
        {
            int i = 0, last_bit = 0, value = 0;
            for (int j = 0; j < m_bits.Count; j++)
            {
                int bit = m_bits[j];
                if (bit == 0)
                {
                    m_result[i] = (i > 0) ? value ^ m_result[i - 1] : value;
                    value = 0;
                    last_bit = 0;
                    i++;
                    continue;
                }
                bit += last_bit;
                last_bit = bit;
                value |= 1 << (bit - 1);
            }
        }

        bool ReadNormalBits(BitStringReader reader)
        {
            int i = 0;
            while (i < m_result.Count)
            {
                int bit = (int)reader.Read(kNormalBits);
                if (bit == 0)
                {
                    i++;
                }
                m_bits.Add((byte)bit);
            }

            return true;
        }

        bool ReadExceptionBits(BitStringReader reader)
        {
            for (int i = 0; i < m_bits.Count; i++)
            {
                if (m_bits[i] == kMaxNormalValue)
                {
                    if (reader.Eof)
                    {
                        // Invalid fingerprint (reached EOF while reading exception bits)
                        return false;
                    }

                    m_bits[i] += (byte)reader.Read(kExceptionBits);
                }
            }

            return true;
        }
    }
}
