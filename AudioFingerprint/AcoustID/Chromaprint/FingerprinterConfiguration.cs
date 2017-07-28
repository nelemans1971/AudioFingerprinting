// -----------------------------------------------------------------------
// <copyright file="FingerprinterConfiguration.cs" company="">
// Original C++ implementation by Lukas Lalinsky, http://acoustid.org/chromaprint
// </copyright>
// -----------------------------------------------------------------------

namespace AcoustID.Chromaprint
{
    /// <summary>
    /// TODO: Update summary.
    /// </summary>
    class FingerprinterConfiguration
    {
        protected static readonly double[] kChromaFilterCoefficients = { 0.25, 0.75, 1.0, 0.75, 0.25 };

        protected Classifier[] m_classifiers; // TODO: access
        protected double[] m_filter_coefficients;
        protected bool m_interpolate;
        protected bool m_remove_silence;
        protected int m_silence_threshold;

        public Classifier[] Classifiers
        {
            get { return m_classifiers; }
            //set { m_classifiers = value; }
        }

        public double[] FilterCoefficients
        {
            get { return m_filter_coefficients; }
            //set { m_filter_coefficients = value; }
        }

        public bool Interpolate
        {
            get { return m_interpolate; }
            //set { m_interpolate = value; }
        }

        public bool RemoveSilence
        {
            get { return m_remove_silence; }
            //set { m_remove_silence = value; }
        }

        public int SilenceThreshold
        {
            get { return m_silence_threshold; }
            //set { m_silence_threshold = value; }
        }


        public static FingerprinterConfiguration CreateConfiguration(int algorithm)
        {
            // TODO: create configuration in a safer way

            // OK, this is bad, but ... whatever ...
            switch (algorithm)
            {
                case 0: // ChromaprintAlgorithm.TEST1
                    return new FingerprinterConfigurationTest1();
                case 1: // ChromaprintAlgorithm.TEST2
                    return new FingerprinterConfigurationTest2();
                case 2: // ChromaprintAlgorithm.TEST3
                    return new FingerprinterConfigurationTest3();
                case 3: // ChromaprintAlgorithm.TEST4
                    return new FingerprinterConfigurationTest4();
            }

            return null;
        }
    }


    // Used for http://oxygene.sk/lukas/2010/07/introducing-chromaprint/
    // Trained on a randomly selected test data
    class FingerprinterConfigurationTest1 : FingerprinterConfiguration
    {
        static Classifier[] kClassifiersTest1 = {
	        new Classifier(new Filter(0, 0, 3, 15), new Quantizer(2.10543, 2.45354, 2.69414)),
	        new Classifier(new Filter(1, 0, 4, 14), new Quantizer(-0.345922, 0.0463746, 0.446251)),
	        new Classifier(new Filter(1, 4, 4, 11), new Quantizer(-0.392132, 0.0291077, 0.443391)),
	        new Classifier(new Filter(3, 0, 4, 14), new Quantizer(-0.192851, 0.00583535, 0.204053)),
	        new Classifier(new Filter(2, 8, 2, 4), new Quantizer(-0.0771619, -0.00991999, 0.0575406)),
	        new Classifier(new Filter(5, 6, 2, 15), new Quantizer(-0.710437, -0.518954, -0.330402)),
	        new Classifier(new Filter(1, 9, 2, 16), new Quantizer(-0.353724, -0.0189719, 0.289768)),
	        new Classifier(new Filter(3, 4, 2, 10), new Quantizer(-0.128418, -0.0285697, 0.0591791)),
	        new Classifier(new Filter(3, 9, 2, 16), new Quantizer(-0.139052, -0.0228468, 0.0879723)),
	        new Classifier(new Filter(2, 1, 3, 6), new Quantizer(-0.133562, 0.00669205, 0.155012)),
	        new Classifier(new Filter(3, 3, 6, 2), new Quantizer(-0.0267, 0.00804829, 0.0459773)),
	        new Classifier(new Filter(2, 8, 1, 10), new Quantizer(-0.0972417, 0.0152227, 0.129003)),
	        new Classifier(new Filter(3, 4, 4, 14), new Quantizer(-0.141434, 0.00374515, 0.149935)),
	        new Classifier(new Filter(5, 4, 2, 15), new Quantizer(-0.64035, -0.466999, -0.285493)),
	        new Classifier(new Filter(5, 9, 2, 3), new Quantizer(-0.322792, -0.254258, -0.174278)),
	        new Classifier(new Filter(2, 1, 8, 4), new Quantizer(-0.0741375, -0.00590933, 0.0600357))
        };

        public FingerprinterConfigurationTest1()
        {
            m_classifiers = kClassifiersTest1;
            m_filter_coefficients = kChromaFilterCoefficients;
            m_interpolate = false;
        }
    }

    // Trained on 60k pairs based on eMusic samples (mp3)
    class FingerprinterConfigurationTest2 : FingerprinterConfiguration
    {
        static Classifier[] kClassifiersTest2 = {
            new Classifier(new Filter(0, 4, 3, 15), new Quantizer(1.98215, 2.35817, 2.63523)),
            new Classifier(new Filter(4, 4, 6, 15), new Quantizer(-1.03809, -0.651211, -0.282167)),
            new Classifier(new Filter(1, 0, 4, 16), new Quantizer(-0.298702, 0.119262, 0.558497)),
            new Classifier(new Filter(3, 8, 2, 12), new Quantizer(-0.105439, 0.0153946, 0.135898)),
            new Classifier(new Filter(3, 4, 4, 8), new Quantizer(-0.142891, 0.0258736, 0.200632)),
            new Classifier(new Filter(4, 0, 3, 5), new Quantizer(-0.826319, -0.590612, -0.368214)),
            new Classifier(new Filter(1, 2, 2, 9), new Quantizer(-0.557409, -0.233035, 0.0534525)),
            new Classifier(new Filter(2, 7, 3, 4), new Quantizer(-0.0646826, 0.00620476, 0.0784847)),
            new Classifier(new Filter(2, 6, 2, 16), new Quantizer(-0.192387, -0.029699, 0.215855)),
            new Classifier(new Filter(2, 1, 3, 2), new Quantizer(-0.0397818, -0.00568076, 0.0292026)),
            new Classifier(new Filter(5, 10, 1, 15), new Quantizer(-0.53823, -0.369934, -0.190235)),
            new Classifier(new Filter(3, 6, 2, 10), new Quantizer(-0.124877, 0.0296483, 0.139239)),
            new Classifier(new Filter(2, 1, 1, 14), new Quantizer(-0.101475, 0.0225617, 0.231971)),
            new Classifier(new Filter(3, 5, 6, 4), new Quantizer(-0.0799915, -0.00729616, 0.063262)),
            new Classifier(new Filter(1, 9, 2, 12), new Quantizer(-0.272556, 0.019424, 0.302559)),
            new Classifier(new Filter(3, 4, 2, 14), new Quantizer(-0.164292, -0.0321188, 0.0846339)),
        };

        public FingerprinterConfigurationTest2()
        {
            m_classifiers = kClassifiersTest2;
            m_filter_coefficients = kChromaFilterCoefficients;
            m_interpolate = false;
        }
    }

    // Trained on 60k pairs based on eMusic samples with interpolation enabled (mp3)
    class FingerprinterConfigurationTest3 : FingerprinterConfiguration
    {
        static Classifier[] kClassifiersTest3 = {
            new Classifier(new Filter(0, 4, 3, 15), new Quantizer(1.98215, 2.35817, 2.63523)),
            new Classifier(new Filter(4, 4, 6, 15), new Quantizer(-1.03809, -0.651211, -0.282167)),
            new Classifier(new Filter(1, 0, 4, 16), new Quantizer(-0.298702, 0.119262, 0.558497)),
            new Classifier(new Filter(3, 8, 2, 12), new Quantizer(-0.105439, 0.0153946, 0.135898)),
            new Classifier(new Filter(3, 4, 4, 8), new Quantizer(-0.142891, 0.0258736, 0.200632)),
            new Classifier(new Filter(4, 0, 3, 5), new Quantizer(-0.826319, -0.590612, -0.368214)),
            new Classifier(new Filter(1, 2, 2, 9), new Quantizer(-0.557409, -0.233035, 0.0534525)),
            new Classifier(new Filter(2, 7, 3, 4), new Quantizer(-0.0646826, 0.00620476, 0.0784847)),
            new Classifier(new Filter(2, 6, 2, 16), new Quantizer(-0.192387, -0.029699, 0.215855)),
            new Classifier(new Filter(2, 1, 3, 2), new Quantizer(-0.0397818, -0.00568076, 0.0292026)),
            new Classifier(new Filter(5, 10, 1, 15), new Quantizer(-0.53823, -0.369934, -0.190235)),
            new Classifier(new Filter(3, 6, 2, 10), new Quantizer(-0.124877, 0.0296483, 0.139239)),
            new Classifier(new Filter(2, 1, 1, 14), new Quantizer(-0.101475, 0.0225617, 0.231971)),
            new Classifier(new Filter(3, 5, 6, 4), new Quantizer(-0.0799915, -0.00729616, 0.063262)),
            new Classifier(new Filter(1, 9, 2, 12), new Quantizer(-0.272556, 0.019424, 0.302559)),
            new Classifier(new Filter(3, 4, 2, 14), new Quantizer(-0.164292, -0.0321188, 0.0846339)),
        };

        public FingerprinterConfigurationTest3()
        {
            m_classifiers = kClassifiersTest3;
            m_filter_coefficients = kChromaFilterCoefficients;
            m_interpolate = true;
        }
    }

    // Same as v2, but trims leading silence
    class FingerprinterConfigurationTest4 : FingerprinterConfigurationTest2
    {
        public FingerprinterConfigurationTest4()
        {
            m_remove_silence = true;
            m_silence_threshold = 50;
        }
    }
}
