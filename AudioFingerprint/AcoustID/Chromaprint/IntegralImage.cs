// -----------------------------------------------------------------------
// <copyright file="IntegralImage.cs" company="">
// Original C++ implementation by Lukas Lalinsky, http://acoustid.org/chromaprint
// </copyright>
// -----------------------------------------------------------------------

namespace AcoustID.Chromaprint
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// Image transformation that allows us to quickly calculate the sum of values 
    /// in a rectangular area.
    /// </summary>
    /// <remarks>
    /// http://en.wikipedia.org/wiki/Summed_area_table
    /// </remarks>
    internal class IntegralImage
    {
        private Image m_image;

        // Construct the integral image. Note that will modify the original
        // image in-place, so it will not be usable afterwards.
        public IntegralImage(Image image)
        {
            m_image = image;
            Transform();
        }

        //! Number of columns in the image
        public int NumColumns() { return m_image.Columns; }

        //! Number of rows in the image
        public int NumRows() { return m_image.Rows; }

        public double[] Row(int i)
        {
            return m_image.Row(i);
        }

        public double[] this[int i]
        {
            get { return m_image.Row(i); }
        }

        public double Area(int x1, int y1, int x2, int y2)
        {
            if (x2 < x1 || y2 < y1)
            {
                // TODO: throw?
                return 0.0;
            }

            double area = m_image.Get(x2, y2);
            if (x1 > 0)
            {
                area -= m_image.Get(x1 - 1, y2);
                if (y1 > 0)
                {
                    area += m_image.Get(x1 - 1, y1 - 1);
                }
            }
            if (y1 > 0)
            {
                area -= m_image.Get(x2, y1 - 1);
            }
            //std::cout << "Area("<<x1<<","<<y1<<","<<x2<<","<<y2<<") = "<<area<<"\n";
            return area;
        }


        private void Transform()
        {
            int num_rows = m_image.Rows;
            int num_columns = m_image.Columns;

            double[] data = m_image.Data;

            int current = 1;
            int last = 0;

            for (int m = 1; m < num_columns; m++)
            {
                // First column - add value on top
                data[current] = data[current] + data[current - 1];
                ++current;
            }
            for (int n = 1; n < num_rows; n++)
            {
                // First row - add value on left
                data[current] = data[current] + data[last];
                ++current;
                ++last;
                // Add values on left, up and up-left
                for (int m = 1; m < num_columns; m++)
                {
                    data[current] = data[current] + data[current - 1] + data[last] - data[last - 1];
                    ++current;
                    ++last;
                }
            }
        }
    }
}
