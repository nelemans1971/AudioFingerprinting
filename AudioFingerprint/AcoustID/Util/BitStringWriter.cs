// -----------------------------------------------------------------------
// <copyright file="BitStringWriter.cs" company="">
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
    /// Write bits to a string.
    /// </summary>
    internal class BitStringWriter
    {
        List<byte> m_value;
        uint m_buffer;
        int m_buffer_size;

        public byte[] Bytes
        {
            get { return m_value.ToArray(); }
        }

        public string Value
        {
            get { return Base64.ByteEncoding.GetString(m_value.ToArray()); }
        }

        public BitStringWriter()
        {
            m_value = new List<byte>();
            m_buffer = 0;
            m_buffer_size = 0;
        }

        public void Write(uint x, int bits)
        {
            m_buffer |= (x << m_buffer_size);
            m_buffer_size += bits;
            while (m_buffer_size >= 8)
            {
                m_value.Add((byte)(m_buffer & 255));
                m_buffer >>= 8;
                m_buffer_size -= 8;
            }
        }

        public void Flush()
        {
            while (m_buffer_size > 0)
            {
                m_value.Add((byte)(m_buffer & 255));
                m_buffer >>= 8;
                m_buffer_size -= 8;
            }
            m_buffer_size = 0;
        }
    }
}
