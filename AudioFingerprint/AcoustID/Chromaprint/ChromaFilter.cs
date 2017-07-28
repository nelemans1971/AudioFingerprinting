// -----------------------------------------------------------------------
// <copyright file="ChromaFilter.cs" company="">
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
    internal class ChromaFilter : IFeatureVectorConsumer
    {
        double[] m_coefficients;
        int m_length;
        double[][] m_buffer;
        double[] m_result;
        int m_buffer_offset;
        int m_buffer_size;
        IFeatureVectorConsumer m_consumer;


        public ChromaFilter(double[] coefficients, IFeatureVectorConsumer consumer)
        {
            m_coefficients = coefficients;
            m_length = coefficients.Length;
            m_buffer = new double[8][];
            m_result = new double[12];
            m_buffer_offset = 0;
            m_buffer_size = 1;
            m_consumer = consumer;
        }

        public void Reset()
        {
            m_buffer_size = 1;
            m_buffer_offset = 0;
        }

        public void Consume(double[] features)
        {
            m_buffer[m_buffer_offset] = features;
            m_buffer_offset = (m_buffer_offset + 1) % 8;
            if (m_buffer_size >= m_length)
            {
                int offset = (m_buffer_offset + 8 - m_length) % 8;

                for (int i = 0; i < m_result.Length; i++)
                {
                    m_result[i] = 0.0;
                }
                //fill(m_result.begin(), m_result.end(), 0.0);
                for (int i = 0; i < 12; i++)
                {
                    for (int j = 0; j < m_length; j++)
                    {
                        m_result[i] += m_buffer[(offset + j) % 8][i] * m_coefficients[j];
                    }
                }
                m_consumer.Consume(m_result);
            }
            else
            {
                m_buffer_size++;
            }
        }
    }
}
