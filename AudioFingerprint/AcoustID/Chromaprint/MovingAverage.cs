// -----------------------------------------------------------------------
// <copyright file="MovingAverage.cs" company="">
// Original C++ implementation by Lukas Lalinsky, http://acoustid.org/chromaprint
// </copyright>
// -----------------------------------------------------------------------

namespace AcoustID.Chromaprint
{
    /// <summary>
    /// TODO: Update summary.
    /// </summary>
    internal class MovingAverage
    {
        private short[] m_buffer;
        private int m_size;
        private int m_offset;
        private int m_sum;
        private int m_count;

        public MovingAverage(int size)
        {
            m_size = size;
            m_offset = 0;
            m_sum = 0;
            m_count = 0;
            m_buffer = new short[m_size];
            //std::fill(m_buffer, m_buffer + m_size, 0);
        }

        ~MovingAverage()
        {
            //delete[] m_buffer;
        }

        public void AddValue(short x)
        {
            //DEBUG() << "offset is " << m_offset << "\n";
            m_sum += x;
            //DEBUG() << "adding " << x << " sum is " << m_sum << "\n";
            m_sum -= m_buffer[m_offset];
            //DEBUG() << "subtracting " << m_buffer[m_offset] << " sum is " << m_sum << "\n";
            if (m_count < m_size)
            {
                m_count++;
            }
            m_buffer[m_offset] = x;
            m_offset = (m_offset + 1) % m_size;
        }

        public int GetAverage()
        {
            if (m_count == 0)
            {
                return 0;
            }

            return m_sum / m_count;
        }
    }
}
