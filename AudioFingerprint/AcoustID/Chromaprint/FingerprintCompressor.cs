// -----------------------------------------------------------------------
// <copyright file="FingerprintCompressor.cs" company="">
// Original C++ implementation by Lukas Lalinsky, http://acoustid.org/chromaprint
// </copyright>
// -----------------------------------------------------------------------

namespace AcoustID.Chromaprint
{
    using System;
    using System.Collections.Generic;
    using AcoustID.Util;

    /// <summary>
    /// TODO: Update summary.
    /// </summary>
    public class FingerprintCompressor
    {
        static readonly int MaxNormalValue = 7;
        static readonly int NormalBits = 3;
        static readonly int ExceptionBits = 5;

        List<byte> m_result;
        List<byte> m_bits = new List<byte>();

        public string Compress(int[] data, int algorithm = 0)
        {
            if (data.Length > 0)
            {
                ProcessSubfingerprint((uint)data[0]);
                for (int i = 1; i < data.Length; i++)
                {
                    ProcessSubfingerprint((uint)data[i] ^ (uint)data[i - 1]);
                }
            }
            int length = data.Length;
            m_result = new List<byte>();
            m_result.Add((byte)((int)algorithm & 255));
            m_result.Add((byte)((length >> 16) & 255));
            m_result.Add((byte)((length >> 8) & 255));
            m_result.Add((byte)((length) & 255));
            WriteNormalBits();
            WriteExceptionBits();

            return Base64.ByteEncoding.GetString(m_result.ToArray());
        }

        void ProcessSubfingerprint(uint x)
        {
            int bit = 1, last_bit = 0;
            while (x != 0)
            {
                if ((x & 1) != 0)
                {
                    m_bits.Add((byte)(bit - last_bit));
                    last_bit = bit;
                }
                x >>= 1;
                bit++;
            }
            m_bits.Add((byte)0);
        }

        void WriteNormalBits()
        {
            BitStringWriter writer = new BitStringWriter();
            for (int i = 0; i < m_bits.Count; i++)
            {
                writer.Write((uint)Math.Min((int)m_bits[i], MaxNormalValue), NormalBits);
            }
            writer.Flush();
            m_result.AddRange(writer.Bytes);
        }

        void WriteExceptionBits()
        {
            BitStringWriter writer = new BitStringWriter();
            for (int i = 0; i < m_bits.Count; i++)
            {
                if (m_bits[i] >= MaxNormalValue)
                {
                    writer.Write((uint)((int)m_bits[i] - MaxNormalValue), ExceptionBits);
                }
            }
            writer.Flush();
            m_result.AddRange(writer.Bytes);
        }
    }
}
