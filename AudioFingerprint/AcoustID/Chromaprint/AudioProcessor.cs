// -----------------------------------------------------------------------
// <copyright file="AudioProcessor.cs" company="">
// Original C++ implementation by Lukas Lalinsky, http://acoustid.org/chromaprint
// </copyright>
// -----------------------------------------------------------------------

namespace AcoustID.Chromaprint
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Diagnostics;
    using AcoustID.Audio;

    /// <summary>
    /// Audio processor with multi-channel to mono converting and resampling. Passes
    /// the pre-processed data on to a given consumer.
    /// </summary>
    internal class AudioProcessor : IAudioConsumer
    {
        private static readonly int kMinSampleRate = 1000;
        private static readonly int kMaxBufferSize = 1024 * 16;

        // Resampler configuration
        private static readonly int kResampleFilterLength = 16;
        private static readonly int kResamplePhaseCount = 10;
        private static readonly bool kResampleLinear = false;
        private static readonly double kResampleCutoff = 0.8;

        private short[] m_buffer;
        private short[] m_resample_buffer;
        private int m_buffer_offset;
        private int m_buffer_size;
        private int m_target_sample_rate;
        private int m_num_channels;
        private IAudioConsumer m_consumer;

        private Resampler m_resample_ctx;

        public AudioProcessor(int sample_rate, IAudioConsumer consumer)
        {
            m_buffer_size = kMaxBufferSize;
            m_target_sample_rate = sample_rate;
            m_consumer = consumer;

            m_buffer = new short[kMaxBufferSize];
            m_buffer_offset = 0;
            m_resample_buffer = new short[kMaxBufferSize];
        }

        public int TargetSampleRate
        {
            get { return m_target_sample_rate; }
            set { m_target_sample_rate = value; }
        }

        public IAudioConsumer Consumer
        {
            get { return m_consumer; }
            set { m_consumer = value; }
        }

        #region Public methods

        //! Prepare for a new audio stream
        public bool Reset(int sample_rate, int num_channels)
        {
            if (num_channels <= 0)
            {
                Debug.WriteLine("Chromaprint::AudioProcessor::Reset() -- No audio channels.");
                return false;
            }

            if (sample_rate <= kMinSampleRate)
            {
                Debug.WriteLine("Chromaprint::AudioProcessor::Reset() -- Sample rate less than {0} ({1}).",
                    kMinSampleRate, sample_rate);
                return false;
            }

            m_buffer_offset = 0;

            if (m_resample_ctx != null)
            {
                m_resample_ctx.Close();
                m_resample_ctx = null;
            }

            if (sample_rate != m_target_sample_rate)
            {
                m_resample_ctx = new Resampler();
                m_resample_ctx.Init(
                    m_target_sample_rate, sample_rate,
                    kResampleFilterLength,
                    kResamplePhaseCount,
                    kResampleLinear,
                    kResampleCutoff);

            }

            m_num_channels = num_channels;
            return true;
        }

        //! Process a chunk of data from the audio stream
        public void Consume(short[] input, int length)
        {
            if (length < 0 || length % m_num_channels != 0)
            {
                throw new ArgumentException("input length");
            }

            int offset = 0;

            length /= m_num_channels;
            while (length > 0)
            {
                int consumed = Load(input, offset, length);
                offset += consumed * m_num_channels;
                length -= consumed;
                if (m_buffer_size == m_buffer_offset)
                {
                    Resample();
                    if (m_buffer_size == m_buffer_offset)
                    {
                        Debug.WriteLine("Chromaprint::AudioProcessor::Consume() -- Resampling failed?");
                        return;
                    }
                }
            }
        }

        //! Process any buffered input that was not processed before and clear buffers
        public void Flush()
        {
            if (m_buffer_offset > 0)
            {
                Resample();
            }
        }

        #endregion

        #region Private methods

        int Load(short[] input, int offset, int length)
        {
            if (length < 0 || m_buffer_offset > m_buffer_size)
            {
                throw new Exception();
            }

            length = Math.Min(length, m_buffer_size - m_buffer_offset);
            switch (m_num_channels)
            {
                case 1:
                    LoadMono(input, offset, length);
                    break;
                case 2:
                    LoadStereo(input, offset, length);
                    break;
                default:
                    LoadMultiChannel(input, offset, length);
                    break;
            }
            m_buffer_offset += length;
            return length;
        }

        void LoadMono(short[] input, int offset, int length)
        {
            int i = m_buffer_offset;
            int j = 0;

            while (length-- > 0)
            {
                m_buffer[i + j++] = input[offset];
                offset++;
            }
        }

        void LoadStereo(short[] input, int offset, int length)
        {
            int i = m_buffer_offset;
            int j = 0;

            while (length-- > 0)
            {
                m_buffer[i + j++] = (short)((input[offset] + input[offset + 1]) / 2);
                offset += 2;
            }
        }

        void LoadMultiChannel(short[] input, int offset, int length)
        {
            int i = m_buffer_offset;
            int j = 0;

            long sum;

            while (length-- > 0)
            {
                sum = 0;

                for (int c = 0; c < m_num_channels; c++)
                {
                    sum += input[offset++];
                }

                m_buffer[i + j++] = (short)(sum / m_num_channels);
            }
        }

        void Resample()
        {
            if (m_resample_ctx == null)
            {
                m_consumer.Consume(m_buffer, m_buffer_offset);
                m_buffer_offset = 0;
                return;
            }

            int consumed = 0;
            int length = m_resample_ctx.Resample(m_resample_buffer, m_buffer, ref consumed, m_buffer_offset, kMaxBufferSize, true);
            if (length > kMaxBufferSize)
            {
                //DEBUG() << "Chromaprint::AudioProcessor::Resample() -- Resampling overwrote output buffer.\n";
                length = kMaxBufferSize;
            }
            m_consumer.Consume(m_resample_buffer, length);
            int remaining = m_buffer_offset - consumed;
            if (remaining > 0)
            {
                Array.Copy(m_buffer, consumed, m_buffer, 0, m_buffer_offset - consumed);
                //copy(m_buffer + consumed, m_buffer + m_buffer_offset, m_buffer);
            }
            else if (remaining < 0)
            {
                //DEBUG() << "Chromaprint::AudioProcessor::Resample() -- Resampling overread input buffer.\n";
                remaining = 0;
            }
            m_buffer_offset = remaining;
        }

        #endregion
    }
}
