// -----------------------------------------------------------------------
// <copyright file="Image.cs" company="">
// Original C++ implementation by Lukas Lalinsky, http://acoustid.org/chromaprint
// </copyright>
// -----------------------------------------------------------------------

namespace AcoustID.Chromaprint
{
    using System;

    /// <summary>
    /// TODO: Update summary.
    /// </summary>
    public class Image
    {
        private int m_columns;
        private double[] m_data;

        public int Columns
        {
            get { return m_columns; }
        }

        public int Rows
        {
            get { return m_data.Length / m_columns; }
        }

        public double this[int i, int j]
        {
            get { return m_data[m_columns * i + j]; }
            set { m_data[m_columns * i + j] = value; }
        }

        internal double[] Data
        {
            get { return m_data; }
        }

        public Image(int columns)
            : this(columns, 0)
        {
        }

        public Image(int columns, int rows)
        {
            m_columns = columns;
            m_data = new double[columns * rows];
        }

        public Image(int columns, double[] data)
        {
            m_columns = columns;
            m_data = data;
        }

        internal double Get(int i, int j)
        {
            return m_data[m_columns * i + j];
        }

        internal void Set(int i, int j, double value)
        {
            m_data[m_columns * i + j] = value;
        }

        internal void AddRow(double[] row)
        {
            int n = m_data.Length;
            Array.Resize(ref m_data, n + m_columns);

            for (int i = 0; i < m_columns; i++)
            {
                m_data[n + i] = row[i];
            }
        }

        internal double[] Row(int i)
        {
            //assert(0 <= i && i < NumRows());

            double[] row = new double[m_columns];
            int n = i * m_columns;

            for (int j = 0; j < m_columns; j++)
            {
                row[j] = m_data[n + j];
            }

            return row;
        }
    }
}
