// -----------------------------------------------------------------------
// <copyright file="ChromaNormalizer.cs" company="">
// Original C++ implementation by Lukas Lalinsky, http://acoustid.org/chromaprint
// </copyright>
// -----------------------------------------------------------------------

namespace AcoustID.Chromaprint
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using AcoustID.Util;

    /// <summary>
    /// TODO: Update summary.
    /// </summary>
    internal class ChromaNormalizer : IFeatureVectorConsumer
    {
        IFeatureVectorConsumer m_consumer;

        public ChromaNormalizer(IFeatureVectorConsumer consumer)
        {
            m_consumer = consumer;
        }

        public void Reset()
        {
        }

        public void Consume(double[] features)
        {
            Helper.NormalizeVector(features, Helper.EuclideanNorm(features), 0.01);

            m_consumer.Consume(features);
        }
    }
}
