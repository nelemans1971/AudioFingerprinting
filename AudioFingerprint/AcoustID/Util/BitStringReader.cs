// -----------------------------------------------------------------------
// <copyright file="BitStringReader.cs" company="">
// Original C++ implementation by Lukas Lalinsky, http://acoustid.org/chromaprint
// </copyright>
// -----------------------------------------------------------------------

namespace AcoustID.Util
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// Read bits from a string.
    /// </summary>
    internal class BitStringReader
    {
        byte[] m_value;
        int m_value_iter;
        uint m_buffer;
        int m_buffer_size;
        bool m_eof;

        public bool Eof
        {
            get { return m_eof; }
        }

        public BitStringReader(byte[] input)
        {
            m_value = input;
            m_buffer = 0;
            m_buffer_size = 0;
            m_eof = false;
        }

        public BitStringReader(string input)
            : this(Base64.ByteEncoding.GetBytes(input))
        {
        }

        public uint Read(int bits)
        {
            if (m_buffer_size < bits)
            {
                if (m_value_iter < m_value.Length)
                {
                    m_buffer |= (uint)(m_value[m_value_iter++] << m_buffer_size);
                    m_buffer_size += 8;
                }
                else
                {
                    m_eof = true;
                }
            }

            uint result = (uint)(m_buffer & ((1 << bits) - 1));
            m_buffer >>= bits;
            m_buffer_size -= bits;

            if (m_buffer_size <= 0 && m_value_iter == m_value.Length)
            {
                m_eof = true;
            }

            return result;
        }

        public void Reset()
        {
            m_buffer = 0;
            m_buffer_size = 0;
        }

        public int AvailableBits()
        {
            return m_eof ? 0 : m_buffer_size + 8 * (m_value.Length - m_value_iter);
        }
    }
}
