namespace Lz4Net
{
    public enum Lz4Mode
    {
        /// <summary>
        /// The very fast Lz4 algorithm implemtation.
        /// </summary>
        Fast = 0,
        /// <summary>
        /// A High Compression mode that is slower but with a better compression rate.
        /// </summary>
        HighCompression = 1
    }
}