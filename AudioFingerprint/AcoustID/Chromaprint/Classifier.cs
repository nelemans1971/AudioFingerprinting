// -----------------------------------------------------------------------
// <copyright file="Classifier.cs" company="">
// Original C++ implementation by Lukas Lalinsky, http://acoustid.org/chromaprint
// </copyright>
// -----------------------------------------------------------------------

namespace AcoustID.Chromaprint
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    internal class Classifier
    {
        private Filter m_filter;
        private Quantizer m_quantizer;

        internal Filter Filter
        {
            get { return m_filter; }
        }

        public Classifier()
            : this(new Filter(), new Quantizer(0.0, 0.0, 0.0))
        {
        }

        public Classifier(Filter filter, Quantizer quantizer)
        {
            m_filter = filter;
            m_quantizer = quantizer;
        }

        public int Classify(IntegralImage image, int offset)
        {
            double value = m_filter.Apply(image, offset);
            return m_quantizer.Quantize(value);
        }
    }
}
