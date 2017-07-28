namespace AudioFingerprint.Audio
{
    public class SubFingerprintHash
    {
        public SubFingerprintHash()
            : this(0, 0, 0.0d)
        {
        }

        public SubFingerprintHash(uint subFingerprint, int sequenceNumber, double timestamp)
        {
            this.SubFingerprint = subFingerprint;
            this.SequenceNumber = sequenceNumber;
            this.Timestamp = timestamp;
        }


        public uint SubFingerprint
        {
            get;
            set;
        }

        public int SequenceNumber
        {
            get;
            set;
        }

        public double Timestamp
        {
            get;
            set;
        }
    }
}
