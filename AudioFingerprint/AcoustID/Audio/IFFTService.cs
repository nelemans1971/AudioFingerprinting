// -----------------------------------------------------------------------
// <copyright file="IFFTService.cs" company="">
// Original C++ implementation by Lukas Lalinsky, http://acoustid.org/chromaprint
// </copyright>
// -----------------------------------------------------------------------

namespace AcoustID.Audio
{
    using AcoustID.Chromaprint;

    /// <summary>
    /// Interface for services computing the FFT.
    /// </summary>
    public interface IFFTService
    {
        /// <summary>
        /// Initializes the FFT service.
        /// </summary>
        /// <param name="frame_size">The frame size.</param>
        /// <param name="window">The window.</param>
        void Initialize(int frame_size, double[] window);

        /// <summary>
        /// Gets the FFT of given frame.
        /// </summary>
        /// <param name="input">The input data (time domain audio).</param>
        /// <param name="output">The output data (frequency domain).</param>
        void ComputeFrame(short[] input, double[] output);
    }
}
