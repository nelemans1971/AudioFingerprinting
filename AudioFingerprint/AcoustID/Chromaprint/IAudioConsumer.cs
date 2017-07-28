// -----------------------------------------------------------------------
// <copyright file="IAudioConsumer.cs" company="">
// Original C++ implementation by Lukas Lalinsky, http://acoustid.org/chromaprint
// </copyright>
// -----------------------------------------------------------------------

namespace AcoustID.Chromaprint
{
    /// <summary>
    /// Consumer for 16bit audio data buffer.
    /// </summary>
    public interface IAudioConsumer
    {
        void Consume(short[] input, int length);
    }
}
