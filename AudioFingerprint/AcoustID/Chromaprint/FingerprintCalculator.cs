// -----------------------------------------------------------------------
// <copyright file="FingerprintCalculator.cs" company="">
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
    /// TODO: Update summary.
    /// </summary>
    internal class FingerprintCalculator
    {
        public static uint[] GrayCode = { 0, 1, 3, 2 };

        Classifier[] m_classifiers;
        int m_num_classifiers;
        int m_max_filter_width;

        public FingerprintCalculator(Classifier[] classifiers)
        {
            m_classifiers = classifiers;
            m_num_classifiers = classifiers.Length;
            m_max_filter_width = 0;
            for (int i = 0; i < m_num_classifiers; i++)
            {
                m_max_filter_width = Math.Max(m_max_filter_width, classifiers[i].Filter.Width);
            }

            if (m_max_filter_width == 0)
            {
                throw new Exception("m_max_filter_width");
            }
        }

        public int[] Calculate(Image image)
        {
            int length = image.Rows - m_max_filter_width + 1;
            if (length <= 0)
            {
                //DEBUG() << "Chromaprint::FingerprintCalculator::Calculate() -- Not "
                //		<< "enough data. Image has " << image.NumRows() << " rows, "
                //		<< "needs at least " << m_max_filter_width << " rows.\n";
                return null;
            }
            IntegralImage integral_image = new IntegralImage(image);
            var fingerprint = new int[length];
            for (int i = 0; i < length; i++)
            {
                fingerprint[i] = CalculateSubfingerprint(integral_image, i);
            }
            return fingerprint;
        }

        public int CalculateSubfingerprint(IntegralImage image, int offset)
        {
            uint bits = 0;
            for (int i = 0; i < m_num_classifiers; i++)
            {
                //for (int i = m_num_classifiers - 1; i >= 0; i--) {
                // TODO: cast uint
                bits = (bits << 2) | GrayCode[m_classifiers[i].Classify(image, offset)];
                //bits = (bits << 2) | m_classifiers[i].Classify(image, offset);
            }
            return (int)bits;
        }
    }
}
