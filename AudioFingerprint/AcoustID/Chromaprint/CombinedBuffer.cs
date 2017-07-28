// -----------------------------------------------------------------------
// <copyright file="CombinedBuffer.cs" company="">
// Original C++ implementation by Lukas Lalinsky, http://acoustid.org/chromaprint
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AcoustID.Chromaprint
{
    /// <summary>
    /// Combines two short (Int16) buffers.
    /// </summary>
    internal class CombinedBuffer
    {
        short[][] m_buffer = new short[3][];
        int[] m_size = new int[3];
        int m_offset;

        public CombinedBuffer(short[] buffer1, int size1, short[] buffer2, int size2)
        {
            m_offset = 0;
            m_buffer[0] = buffer1;
            m_buffer[1] = buffer2;
            m_buffer[2] = null;
            m_size[0] = size1;
            m_size[1] = size2;
            m_size[2] = -1;
        }

        /// <summary>
        /// Gets the size of the combined buffer.
        /// </summary>
        public int Size
        {
            get { return m_size[0] + m_size[1] - m_offset; }
        }

        /// <summary>
        /// Gets the current offset of the combined buffer.
        /// </summary>
        public int Offset
        {
            get { return m_offset; }
        }

        /// <summary>
        /// Gets the element at given position.
        /// </summary>
        public short this[int i]
        {
            get
            {
                int k = i + m_offset;
                if (k < m_size[0])
                {
                    return m_buffer[0][k];
                }
                k -= m_size[0];
                return m_buffer[1][k];
            }
        }

        /// <summary>
        /// Shift the buffer offset.
        /// </summary>
        /// <param name="shift">Places to shift.</param>
        /// <returns>The new buffer offset.</returns>
        public int Shift(int shift)
        {
            m_offset += shift;
            return m_offset;
        }

        /// <summary>
        /// Read a number of values from the combined buffer.
        /// </summary>
        /// <param name="buffer">Buffer to write into.</param>
        /// <param name="offset">Offset to start reading.</param>
        /// <param name="length">Number of values to read.</param>
        /// <returns>Total number of values read.</returns>
        public int Read(short[] buffer, int offset, int length)
        {
            int n = length, pos = offset + m_offset;

            if (pos < m_size[0] && pos + length > m_size[0])
            {
                // Number of shorts to be read from first buffer
                int split = m_size[0] - pos;

                // Number of shorts to be read from second buffer
                n = Math.Min(length - split, m_size[1]);

                // Copy from both buffers
                Array.Copy(m_buffer[0], pos, buffer, 0, split);
                Array.Copy(m_buffer[1], 0, buffer, split, n);

                // Correct total length
                n += split;
            }
            else
            {
                if (pos >= m_size[0])
                {
                    pos -= m_size[0];
                    // Number of shorts to be read from second buffer
                    n = Math.Min(length, m_size[1] - pos);

                    // Read from second buffer
                    Array.Copy(m_buffer[1], pos, buffer, 0, n);
                }
                else
                {
                    // Read from first buffer
                    Array.Copy(m_buffer[0], pos, buffer, 0, n);
                }
            }

            return n;
        }

        /// <summary>
        /// Read all remaining values from the buffer.
        /// </summary>
        /// <param name="buffer">Buffer to write into.</param>
        public void Flush(short[] buffer)
        {
            // Read the whole buffer (offset will be taken care of).
            if (this.Size > 0)
            {
                this.Read(buffer, 0, this.Size);
            }
        }
    }
}
