// -----------------------------------------------------------------------
// <copyright file="FFT.cs" company="">
// Original C++ implementation by Lukas Lalinsky, http://acoustid.org/chromaprint
// </copyright>
// -----------------------------------------------------------------------

namespace AcoustID.Chromaprint
{
    using System;
    using AcoustID.Audio;
    using AcoustID.Util;

    /// <summary>
    /// Consumes audio data and passes the FFT of this data on to
    /// a FFT frame consumer.
    /// </summary>
    internal class FFT : IAudioConsumer
    {
        double[] m_window;
        int m_buffer_offset;
        short[] m_buffer;
        FFTFrame m_frame;
        int m_frame_size;
        int m_increment;
        IFFTService m_lib;
        IFFTFrameConsumer m_consumer;

        // FFT input buffer
        short[] m_input;

        public FFT(int frame_size, int overlap, IFFTFrameConsumer consumer)
            : this(frame_size, overlap, consumer, new LomontFFTService())
        {
        }

        public FFT(int frame_size, int overlap, IFFTFrameConsumer consumer, IFFTService fftService)
        {
            m_window = new double[frame_size];
            m_buffer_offset = 0;
            m_buffer = new short[frame_size];
            m_frame = new FFTFrame(frame_size);
            m_frame_size = frame_size;
            m_increment = frame_size - overlap;
            m_consumer = consumer;

            Helper.PrepareHammingWindow(ref m_window, 0, frame_size);
            for (int i = 0; i < frame_size; i++)
            {
                m_window[i] /= short.MaxValue;
            }

            m_lib = fftService;
            m_lib.Initialize(frame_size, m_window);

            m_input = new short[frame_size];
        }

        public void Reset()
        {
            m_buffer_offset = 0;
        }

        public void Consume(short[] input, int length)
        {
            // Special case, just pre-filling the buffer
            if (m_buffer_offset + length < m_frame_size)
            {
                Array.Copy(input, 0, m_buffer, m_buffer_offset, length);
                m_buffer_offset += length;
                return;
            }

            // Apply FFT on the available data
            CombinedBuffer combined_buffer = new CombinedBuffer(m_buffer, m_buffer_offset, input, length);

            while (combined_buffer.Size >= m_frame_size)
            {
                combined_buffer.Read(m_input, 0, m_frame_size);
                m_lib.ComputeFrame(m_input, m_frame.Data);

                m_consumer.Consume(m_frame);
                combined_buffer.Shift(m_increment);
            }

            // Copy the remaining input data to the internal buffer
            combined_buffer.Flush(m_buffer);

            m_buffer_offset = combined_buffer.Size;
        }
    }
}
