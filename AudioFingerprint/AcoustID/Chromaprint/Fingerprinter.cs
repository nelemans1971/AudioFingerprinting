// -----------------------------------------------------------------------
// <copyright file="Fingerprinter.cs" company="">
// Original C++ implementation by Lukas Lalinsky, http://acoustid.org/chromaprint
// </copyright>
// -----------------------------------------------------------------------

namespace AcoustID.Chromaprint
{
    using System;
    using AcoustID.Audio;

    /// <summary>
    /// TODO: Update summary.
    /// </summary>
    internal class Fingerprinter : IAudioConsumer
    {
        static readonly int SAMPLE_RATE = 11025;
        static readonly int FRAME_SIZE = 4096;
        static readonly int OVERLAP = FRAME_SIZE - FRAME_SIZE / 3;
        static readonly int MIN_FREQ = 28;
        static readonly int MAX_FREQ = 3520;

        Image m_image;
        ImageBuilder m_image_builder;
        Chroma m_chroma;
        ChromaNormalizer m_chroma_normalizer;
        ChromaFilter m_chroma_filter;
        FFT m_fft;
        AudioProcessor m_audio_processor;
        FingerprintCalculator m_fingerprint_calculator;
        FingerprinterConfiguration m_config;
        SilenceRemover m_silence_remover;

        public Fingerprinter(FingerprinterConfiguration config)
        {
            m_image = new Image(12);
            if (config == null)
            {
                config = new FingerprinterConfigurationTest1();
            }
            m_image_builder = new ImageBuilder(m_image);
            m_chroma_normalizer = new ChromaNormalizer(m_image_builder);
            m_chroma_filter = new ChromaFilter(config.FilterCoefficients, m_chroma_normalizer);
            m_chroma = new Chroma(MIN_FREQ, MAX_FREQ, FRAME_SIZE, SAMPLE_RATE, m_chroma_filter);
            //m_chroma.set_interpolate(true);

            // TODO: inject IFFTService
            m_fft = new FFT(FRAME_SIZE, OVERLAP, m_chroma);
            if (config.RemoveSilence)
            {
                m_silence_remover = new SilenceRemover(m_fft);
                m_silence_remover.Threshold = config.SilenceThreshold;
                m_audio_processor = new AudioProcessor(SAMPLE_RATE, m_silence_remover);
            }
            else
            {
                m_silence_remover = null;
                m_audio_processor = new AudioProcessor(SAMPLE_RATE, m_fft);
            }
            m_fingerprint_calculator = new FingerprintCalculator(config.Classifiers);
            m_config = config;
        }

        public bool SetOption(string name, int value)
        {
            if (name.Equals("silence_threshold"))
            {
                if (m_silence_remover != null)
                {
                    m_silence_remover.Threshold = value;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Initialize the fingerprinting process.
        /// </summary>
        public bool Start(int sample_rate, int num_channels)
        {
            if (!m_audio_processor.Reset(sample_rate, num_channels))
            {
                // TODO: save error message somewhere
                return false;
            }
            m_fft.Reset();
            m_chroma.Reset();
            m_chroma_filter.Reset();
            m_chroma_normalizer.Reset();
            m_image = new Image(12);
            m_image_builder.Reset(m_image);

            return true;
        }

        /// <summary>
        /// Process a block of raw audio data. Call this method as many times
        /// as you need.
        /// </summary>
        public void Consume(short[] samples, int length)
        {
            if (length < 0)
            {
                throw new ArgumentException("length");
            }

            m_audio_processor.Consume(samples, length);
        }

        /// <summary>
        /// Calculate the fingerprint based on the provided audio data.
        /// </summary>
        public int[] Finish()
        {
            m_audio_processor.Flush();
            return m_fingerprint_calculator.Calculate(m_image);
        }
    }
}
