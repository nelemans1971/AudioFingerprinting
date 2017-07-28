// -----------------------------------------------------------------------
// <copyright file="IDecoder.cs" company="">
// Original C++ implementation by Lukas Lalinsky, http://acoustid.org/chromaprint
// </copyright>
// -----------------------------------------------------------------------

namespace AcoustID.Audio
{
    using System;
    using AcoustID.Chromaprint;

    /// <summary>
    /// Interface for audio decoders.
    /// </summary>
    public interface IDecoder
    {
        /// <summary>
        /// Gets the sample rate of the audio sent to the fingerprinter. 
        /// </summary>
        /// <remarks>
        /// May be different from the source audio sample rate, if the decoder does resampling.
        /// </remarks>
        int SampleRate { get; }

        /// <summary>
        /// Gets the channel count of the audio sent to the fingerprinter. 
        /// </summary>
        /// <remarks>
        /// May be different from the source audio channel count.
        /// </remarks>
        int Channels { get; }

        /// <summary>
        /// Decode audio file.
        /// </summary>
        /// <param name="consumer">The <see cref="IAudioConsumer"/> that consumes the decoded audio.</param>
        /// <param name="maxLength">The number of seconds to decode.</param>
        /// <returns>Returns true, if decoding was successful.</returns>
        bool Decode(IAudioConsumer consumer, int maxLength);
    }
}
