// -----------------------------------------------------------------------
// <copyright file="Filter.cs" company="">
// Original C++ implementation by Lukas Lalinsky, http://acoustid.org/chromaprint
// </copyright>
// -----------------------------------------------------------------------

namespace AcoustID.Chromaprint
{
    using System;

    /// <summary>
    /// TODO: Update summary.
    /// </summary>
    internal class Filter
    {
        int m_type;
        int m_y;
        int m_height;
        int m_width;

        public int Type
        {
            get { return m_type; }
            set { m_type = value; }
        }

        public int Y
        {
            get { return m_y; }
            set { m_y = value; }
        }

        public int Height
        {
            get { return m_height; }
            set { m_height = value; }
        }

        public int Width
        {
            get { return m_width; }
            set { m_width = value; }
        }

        public Filter(int type = 0, int y = 0, int height = 0, int width = 0)
        {
            m_type = type;
            m_y = y;
            m_height = height;
            m_width = width;
        }

        public double Apply(IntegralImage image, int x)
        {
            Func<double, double, double> comparer = SubtractLog;

            switch (m_type)
            {
                case 0:
                    return Filter0(image, x, m_y, m_width, m_height, comparer);
                case 1:
                    return Filter1(image, x, m_y, m_width, m_height, comparer);
                case 2:
                    return Filter2(image, x, m_y, m_width, m_height, comparer);
                case 3:
                    return Filter3(image, x, m_y, m_width, m_height, comparer);
                case 4:
                    return Filter4(image, x, m_y, m_width, m_height, comparer);
                case 5:
                    return Filter5(image, x, m_y, m_width, m_height, comparer);
            }
            return 0.0;
        }

        public static double Subtract(double a, double b)
        {
            return a - b;
        }

        public static double SubtractLog(double a, double b)
        {
            double r = Math.Log(1.0 + a) - Math.Log(1.0 + b);

            if (double.IsNaN(r))
            {
                throw new Exception("NaN");
            }

            return r;
        }

        // oooooooooooooooo
        // oooooooooooooooo
        // oooooooooooooooo
        // oooooooooooooooo
        public static double Filter0(IntegralImage image, int x, int y, int w, int h, Func<double, double, double> cmp)
        {
            //Debug.Assert(x >= 0);
            //Debug.Assert(y >= 0);
            //Debug.Assert(w >= 1);
            //Debug.Assert(h >= 1);

            double a = image.Area(x, y, x + w - 1, y + h - 1);
            double b = 0;
            return cmp(a, b);
        }

        // ................
        // ................
        // oooooooooooooooo
        // oooooooooooooooo
        public static double Filter1(IntegralImage image, int x, int y, int w, int h, Func<double, double, double> cmp)
        {
            //Debug.Assert(x >= 0);
            //Debug.Assert(y >= 0);
            //Debug.Assert(w >= 1);
            //Debug.Assert(h >= 1);

            int h_2 = h / 2;

            double a = image.Area(x, y + h_2, x + w - 1, y + h - 1);
            double b = image.Area(x, y, x + w - 1, y + h_2 - 1);

            return cmp(a, b);
        }

        // .......ooooooooo
        // .......ooooooooo
        // .......ooooooooo
        // .......ooooooooo
        public static double Filter2(IntegralImage image, int x, int y, int w, int h, Func<double, double, double> cmp)
        {
            //Debug.Assert(x >= 0);
            //Debug.Assert(y >= 0);
            //Debug.Assert(w >= 1);
            //Debug.Assert(h >= 1);

            int w_2 = w / 2;

            double a = image.Area(x + w_2, y, x + w - 1, y + h - 1);
            double b = image.Area(x, y, x + w_2 - 1, y + h - 1);

            return cmp(a, b);
        }

        // .......ooooooooo
        // .......ooooooooo
        // ooooooo.........
        // ooooooo.........
        public static double Filter3(IntegralImage image, int x, int y, int w, int h, Func<double, double, double> cmp)
        {
            //Debug.Assert(x >= 0);
            //Debug.Assert(y >= 0);
            //Debug.Assert(w >= 1);
            //Debug.Assert(h >= 1);

            int w_2 = w / 2;
            int h_2 = h / 2;

            double a = image.Area(x, y + h_2, x + w_2 - 1, y + h - 1) +
                       image.Area(x + w_2, y, x + w - 1, y + h_2 - 1);
            double b = image.Area(x, y, x + w_2 - 1, y + h_2 - 1) +
                       image.Area(x + w_2, y + h_2, x + w - 1, y + h - 1);

            return cmp(a, b);
        }

        // ................
        // oooooooooooooooo
        // ................
        public static double Filter4(IntegralImage image, int x, int y, int w, int h, Func<double, double, double> cmp)
        {
            //Debug.Assert(x >= 0);
            //Debug.Assert(y >= 0);
            //Debug.Assert(w >= 1);
            //Debug.Assert(h >= 1);

            int h_3 = h / 3;

            double a = image.Area(x, y + h_3, x + w - 1, y + 2 * h_3 - 1);
            double b = image.Area(x, y, x + w - 1, y + h_3 - 1) +
                       image.Area(x, y + 2 * h_3, x + w - 1, y + h - 1);

            return cmp(a, b);
        }

        // .....oooooo.....
        // .....oooooo.....
        // .....oooooo.....
        // .....oooooo.....
        public static double Filter5(IntegralImage image, int x, int y, int w, int h, Func<double, double, double> cmp)
        {
            //Debug.Assert(x >= 0);
            //Debug.Assert(y >= 0);
            //Debug.Assert(w >= 1);
            //Debug.Assert(h >= 1);

            int w_3 = w / 3;

            double a = image.Area(x + w_3, y, x + 2 * w_3 - 1, y + h - 1);
            double b = image.Area(x, y, x + w_3 - 1, y + h - 1) +
                       image.Area(x + 2 * w_3, y, x + w - 1, y + h - 1);

            return cmp(a, b);
        }
    }
}
