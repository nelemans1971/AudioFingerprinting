namespace AudioFingerprint.Audio
{
    public class AudioSamples
    {
        public string Origin
        {
            get;
            set;
        }

        public int Channels
        {
            get;
            set;
        }

        public int SampleRate
        {
            get;
            set;
        }

        public int StartInMS
        {
            get;
            set;
        }

        public int DurationInMS
        {
            get;
            set;
        }

        public float[] Samples
        {
            get;
            set;
        }
    }
}
