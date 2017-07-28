// -----------------------------------------------------------------------
// <copyright file="Chromagram.cs" company="">
// Original C++ implementation by Lukas Lalinsky, http://acoustid.org/chromaprint
// </copyright>
// -----------------------------------------------------------------------

namespace AcoustID.Util
{
    using AcoustID.Audio;
    using AcoustID.Chromaprint;

    /// <summary>
    /// TODO: Update summary.
    /// </summary>
    public class Chromagram
    {
        static int SAMPLE_RATE = 11025;
        static int FRAME_SIZE = 4096;
        static int OVERLAP = FRAME_SIZE - FRAME_SIZE / 3;// 2720;
        static int MIN_FREQ = 28;
        static int MAX_FREQ = 3520;
        //static int MAX_FILTER_WIDTH = 20;

        static double[] ChromaFilterCoefficients = { 0.25, 0.75, 1.0, 0.75, 0.25 };

        public static Image Compute(string file, IDecoder decoder)
        {
            Image image = new Image(12);
            ImageBuilder image_builder = new ImageBuilder(image);
            ChromaNormalizer chroma_normalizer = new ChromaNormalizer(image_builder);
            ChromaFilter chroma_filter = new ChromaFilter(ChromaFilterCoefficients, chroma_normalizer);
            //Chroma chroma = new Chroma(MIN_FREQ, MAX_FREQ, FRAME_SIZE, SAMPLE_RATE, &chroma_normalizer);
            Chroma chroma = new Chroma(MIN_FREQ, MAX_FREQ, FRAME_SIZE, SAMPLE_RATE, chroma_filter);
            FFT fft = new FFT(FRAME_SIZE, OVERLAP, chroma);
            AudioProcessor processor = new AudioProcessor(SAMPLE_RATE, fft);

            processor.Reset(decoder.SampleRate, decoder.Channels);
            decoder.Decode(processor, 120);
            processor.Flush();

            //ExportImage(image, name);

            return image;
        }
    }
}
