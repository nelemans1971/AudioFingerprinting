// -----------------------------------------------------------------------
// <copyright file="Spectrogram.cs" company="">
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
    public class Spectrogram
    {
        static int SAMPLE_RATE = 11025;
        static int FRAME_SIZE = 4096;
        static int OVERLAP = FRAME_SIZE - FRAME_SIZE / 3;// 2720;
        static int MIN_FREQ = 28;
        static int MAX_FREQ = 3520;
        //static int MAX_FILTER_WIDTH = 20;

        public static Image Compute(string file, IDecoder decoder)
        {
            int numBands = 72;

            Image image = new Image(numBands);
            ImageBuilder image_builder = new ImageBuilder(image);
            Spectrum chroma = new Spectrum(numBands, MIN_FREQ, MAX_FREQ, FRAME_SIZE, SAMPLE_RATE, image_builder);
            FFT fft = new FFT(FRAME_SIZE, OVERLAP, chroma);
            AudioProcessor processor = new AudioProcessor(SAMPLE_RATE, fft);

            processor.Reset(decoder.SampleRate, decoder.Channels);
            decoder.Decode(processor, 120);
            processor.Flush();

            //ExportImage(image, name, 0.5);

            return image;
        }
    }
}
