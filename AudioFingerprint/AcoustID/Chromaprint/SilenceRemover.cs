// -----------------------------------------------------------------------
// <copyright file="SilenceRemover.cs" company="">
// Original C++ implementation by Lukas Lalinsky, http://acoustid.org/chromaprint
// </copyright>
// -----------------------------------------------------------------------

namespace AcoustID.Chromaprint
{
    using System;

    /// <summary>
    /// TODO: Update summary.
    /// </summary>
    internal class SilenceRemover : IAudioConsumer
    {
        const short kSilenceWindow = 55; // 5 ms as 11025 Hz

        bool m_start;
        int m_threshold;
        MovingAverage m_average;
        IAudioConsumer m_consumer;

        public int Threshold
        {
            get { return m_threshold; }
            set { m_threshold = value; }
        }

        internal IAudioConsumer Consumer
        {
            get { return m_consumer; }
            set { m_consumer = value; }
        }

        public SilenceRemover(IAudioConsumer consumer, int threshold = 0)
        {
            m_start = true;
            m_threshold = threshold;
            m_average = new MovingAverage(kSilenceWindow);
            m_consumer = consumer;
        }

        public void Consume(short[] input, int length)
        {
            int offset = 0, n = length;
            if (m_start)
            {
                while (length > 0)
                {
                    m_average.AddValue(Math.Abs(input[offset]));
                    if (m_average.GetAverage() > m_threshold)
                    {
                        m_start = false;
                        break;
                    }
                    offset++;
                    length--;
                }
            }

            // TODO: workaround pointer magic: shift array data.
            if (offset > 0)
            {
                for (int i = 0; i < n - offset; i++)
                {
                    input[i] = input[i + offset];
                }

                // Not necessary?
                for (int i = n - offset; i < n; i++)
                {
                    input[i] = 0;
                }
            }

            if (length > 0)
            {
                m_consumer.Consume(input, length);
            }
        }

        public bool Reset(int sample_rate, int num_channels)
        {
            if (num_channels != 1)
            {
                //DEBUG() << "Chromaprint::SilenceRemover::Reset() -- Expecting mono audio signal.\n";
                return false;
            }
            m_start = true;
            return true;
        }


        public void Flush()
        {
        }
    }
}
