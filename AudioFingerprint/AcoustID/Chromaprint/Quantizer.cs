// -----------------------------------------------------------------------
// <copyright file="Quantizer.cs" company="">
// Original C++ implementation by Lukas Lalinsky, http://acoustid.org/chromaprint
// </copyright>
// -----------------------------------------------------------------------

namespace AcoustID.Chromaprint
{
    using System;

    /// <summary>
    /// TODO: Update summary.
    /// </summary>
    internal class Quantizer
    {
        double m_t0, m_t1, m_t2;

        public Quantizer(double t0, double t1, double t2)
        {
            m_t0 = t0;
            m_t1 = t1;
            m_t2 = t2;

            if (t0 > t1 || t1 > t2)
            {
                throw new ArgumentException("t0 < t1 < t2");
            }
        }

        public int Quantize(double value)
        {
            if (value < m_t1)
            {
                if (value < m_t0)
                {
                    return 0;
                }
                return 1;
            }
            else
            {
                if (value < m_t2)
                {
                    return 2;
                }
                return 3;
            }
        }
    }
}
