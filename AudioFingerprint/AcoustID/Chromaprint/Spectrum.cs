// -----------------------------------------------------------------------
// <copyright file="Spectrum.cs" company="">
// Original C++ implementation by Lukas Lalinsky, http://acoustid.org/chromaprint
// </copyright>
// -----------------------------------------------------------------------

namespace AcoustID.Chromaprint
{
    using System.Collections.Generic;
    using AcoustID.Util;

    /// <summary>
    /// TODO: Update summary.
    /// </summary>
    internal class Spectrum : IFFTFrameConsumer
    {
        List<int> m_bands;
        double[] m_features;
        IFeatureVectorConsumer m_consumer;

        public Spectrum(int num_bands, int min_freq, int max_freq, int frame_size, int sample_rate, IFeatureVectorConsumer consumer)
        {
            m_bands = new List<int>(num_bands + 1);
            m_features = new double[num_bands];
            m_consumer = consumer;
            PrepareBands(num_bands, min_freq, max_freq, frame_size, sample_rate);
        }

        public void Consume(FFTFrame frame)
        {
            for (int i = 0; i < NumBands(); i++)
            {
                int first = FirstIndex(i);
                int last = LastIndex(i);
                double numerator = 0.0;
                double denominator = 0.0;
                for (int j = first; j < last; j++)
                {
                    double s = frame.Energy(j);
                    numerator += j * s;
                    denominator += s;
                }
                m_features[i] = denominator / (last - first);
            }
            m_consumer.Consume(m_features);
        }

        protected int NumBands()
        {
            return m_bands.Count - 1;
        }

        protected int FirstIndex(int band)
        {
            return m_bands[band];
        }

        protected int LastIndex(int band)
        {
            return m_bands[band + 1];
        }

        private void PrepareBands(int num_bands, int min_freq, int max_freq, int frame_size, int sample_rate)
        {
            double min_bark = Helper.FreqToBark(min_freq);
            double max_bark = Helper.FreqToBark(max_freq);
            double band_size = (max_bark - min_bark) / num_bands;

            int min_index = Helper.FreqToIndex(min_freq, frame_size, sample_rate);
            //int max_index = FreqToIndex(max_freq, frame_size, sample_rate);

            m_bands[0] = min_index;
            double prev_bark = min_bark;

            for (int i = min_index, b = 0; i < frame_size / 2; i++)
            {
                double freq = Helper.IndexToFreq(i, frame_size, sample_rate);
                double bark = Helper.FreqToBark(freq);
                if (bark - prev_bark > band_size)
                {
                    b += 1;
                    prev_bark = bark;
                    m_bands[b] = i;
                    if (b >= num_bands)
                    {
                        break;
                    }
                }
            }
        }
    }
}
