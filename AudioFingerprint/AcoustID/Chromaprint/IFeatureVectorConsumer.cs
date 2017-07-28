// -----------------------------------------------------------------------
// <copyright file="IFeatureVectorConsumer.cs" company="">
// Original C++ implementation by Lukas Lalinsky, http://acoustid.org/chromaprint
// </copyright>
// -----------------------------------------------------------------------

namespace AcoustID.Chromaprint
{
    /// <summary>
    /// TODO: Update summary.
    /// </summary>
    interface IFeatureVectorConsumer
    {
        void Consume(double[] features);
    }
}
