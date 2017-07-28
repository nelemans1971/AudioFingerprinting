// -----------------------------------------------------------------------
// <copyright file="ChromaResampler.cs" company="">
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
    internal class ChromaResampler : IFeatureVectorConsumer
    {
        double[] m_result;
        int m_iteration;
        int m_factor;
        IFeatureVectorConsumer m_consumer;

        internal IFeatureVectorConsumer Consumer
        {
            get { return m_consumer; }
            set { m_consumer = value; }
        }

        public ChromaResampler(int factor, IFeatureVectorConsumer consumer)
        {
            m_result = new double[12]; //0.0
            m_iteration = 0;
            m_factor = factor;
            m_consumer = consumer;
        }

        public void Consume(double[] features)
        {
            for (int i = 0; i < 12; i++)
            {
                m_result[i] += features[i];
            }
            m_iteration += 1;
            if (m_iteration == m_factor)
            {
                for (int i = 0; i < 12; i++)
                {
                    m_result[i] /= m_factor;
                }
                m_consumer.Consume(m_result);
                Reset();
            }
        }

        public void Reset()
        {
            m_iteration = 0;

            for (int i = 0; i < m_result.Length; i++)
            {
                m_result[i] = 0.0;
            }
        }
    }
}
