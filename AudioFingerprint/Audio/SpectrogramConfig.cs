namespace AudioFingerprint.Audio
{
    using System;

    public abstract class SpectrogramConfig
    {
        public static readonly SpectrogramConfig Default = new DefaultSpectrogramConfig();
        private int sampleRate;
        private int overlap;
        private int wdftSize;

        public int SampleRate
        {
            get
            {
                return sampleRate;
            }
            set
            {
                sampleRate = value;
            }
        }
        /// <summary>
        ///   Gets or sets overlap between the consecutively computed spectrum images 
        /// </summary>
        /// <remarks>64 at 5512 sample rate is aproximatelly 11.6ms</remarks>
        public int Overlap
        {
            get
            {
                return overlap;
            }

            set
            {
                if (value <= 0)
                {
                    throw new ArgumentException("Overlap can't be negative", "value");
                }

                overlap = value;
            }
        }

        /// <summary>
        ///   Gets or sets size of the WDFT block, 371 ms
        /// </summary>
        public int WdftSize
        {
            get
            {
                return wdftSize;
            }

            set
            {
                if (value <= 0)
                {
                    throw new ArgumentException("WdftSize can't be negative", "value");
                }

                wdftSize = value;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the algorithm should use dynamic logarithmic base, instead of static
        /// </summary>
        public bool UseDynamicLogBase
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets a value indicating whether audio signal has to be normalized before its spectrum is built
        /// </summary>
        public bool NormalizeSignal
        {
            get;
            set;
        }

    }

    public class DefaultSpectrogramConfig : SpectrogramConfig
    {
        public DefaultSpectrogramConfig()
        {
            SampleRate = 5512; // hz
            Overlap = 64;
            WdftSize = 2048;
            UseDynamicLogBase = false;
            NormalizeSignal = false;
        }
    }
}