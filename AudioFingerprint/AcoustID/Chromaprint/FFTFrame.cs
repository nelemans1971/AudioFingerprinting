// -----------------------------------------------------------------------
// <copyright file="FFTFrame.cs" company="">
// Original C++ implementation by Lukas Lalinsky, http://acoustid.org/chromaprint
// </copyright>
// -----------------------------------------------------------------------

namespace AcoustID.Chromaprint
{
    using System;

    /// <summary>
    /// TODO: Update summary.
    /// </summary>
    internal class FFTFrame
    {
        private double[] m_data;
        private int m_size;

        public int Size
        {
            get { return m_size; }
        }

        public double[] Data
        {
            get { return m_data; }
        }

        public FFTFrame(int size)
        {
            m_size = size;
            m_data = new double[size];
        }

        public double Magnitude(int i)
        {
            return Math.Sqrt(Energy(i));
        }

        public double Energy(int i)
        {
            return m_data[i];
        }
    }
}
