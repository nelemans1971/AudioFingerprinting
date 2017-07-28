// -----------------------------------------------------------------------
// <copyright file="ImageBuilder.cs" company="">
// Original C++ implementation by Lukas Lalinsky, http://acoustid.org/chromaprint
// </copyright>
// -----------------------------------------------------------------------

namespace AcoustID.Chromaprint
{
    /// <summary>
    /// TODO: Update summary.
    /// </summary>
    internal class ImageBuilder : IFeatureVectorConsumer
    {
        Image m_image;

        public Image Image
        {
            get { return m_image; }
            set { m_image = value; }
        }

        public ImageBuilder(Image image)
        {
            m_image = image;
        }

        public void Reset(Image image)
        {
            m_image = image;
        }

        public void Consume(double[] features)
        {
            //assert(features.size() == (size_t)m_image->NumColumns());
            m_image.AddRow(features);
        }
    }
}
