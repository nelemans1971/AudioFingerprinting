// -----------------------------------------------------------------------
// <copyright file="AudioDecoder.cs" company="">
// Christian Woltering, https://github.com/wo80
// </copyright>
// -----------------------------------------------------------------------

namespace AcoustID.Audio
{
    using AcoustID.Chromaprint;

    /// <summary>
    /// Abstract base class for audio decoders
    /// </summary>
    public abstract class AudioDecoder : IAudioDecoder
    {
        protected static readonly int BUFFER_SIZE = 2 * 192000;

        protected int sampleRate;
        protected int channels;

        protected int sourceSampleRate;
        protected int sourceBitDepth;
        protected int sourceChannels;
        protected int duration;

        protected bool ready;

        public int SourceSampleRate
        {
            get { return sourceSampleRate; }
        }

        public int SourceBitDepth
        {
            get { return sourceBitDepth; }
        }

        public int SourceChannels
        {
            get { return sourceChannels; }
        }

        public int Duration
        {
            get { return duration; }
        }

        public bool Ready
        {
            get { return ready; }
        }

        public int SampleRate
        {
            get { return sampleRate; }
        }

        public int Channels
        {
            get { return channels; }
        }

        public abstract void Load(string file);

        public abstract bool Decode(IAudioConsumer consumer, int maxLength);

        public virtual void Dispose()
        {
        }
    }
}
