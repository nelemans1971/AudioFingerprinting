// -----------------------------------------------------------------------
// <copyright file="Util.cs" company="">
// Original C++ implementation by Lukas Lalinsky, http://acoustid.org/chromaprint
// </copyright>
// -----------------------------------------------------------------------

namespace AcoustID.Util
{
    using System;

    /// <summary>
    /// DSP and math helper methods.
    /// </summary>
    internal static class Helper
    {
        public static void PrepareHammingWindow(ref double[] vector, int first, int last)
        {
            int i = 0, max_i = last - first - 1;
            double scale = 2.0 * Math.PI / max_i;
            while (first != last)
            {
                vector[first] = 0.54 - 0.46 * Math.Cos(scale * i++);
                first++;
            }
        }

        public static void ApplyWindow(ref double[] in_output, double[] window, int size, double scale)
        {
            int i = 0;
            while (size-- > 0)
            {
                in_output[i] *= (window[i] * scale);
                ++i;
            }
        }

        public static double Sum(double[] vector, int first, int last)
        {
            double sum = 0;
            while (first != last)
            {
                sum += vector[first];
                ++first;
            }
            return sum;
        }

        public static double EuclideanNorm(double[] vector)
        {
            double squares = 0;

            foreach (var value in vector)
            {
                squares += value * value;
            }

            return (squares > 0) ? Math.Sqrt(squares) : 0;
        }

        public static void NormalizeVector(double[] vector, double norm, double threshold = 0.01)
        {
            if (norm < threshold)
            {
                for (int i = 0; i < vector.Length; i++)
                {
                    vector[i] = 0.0;
                }
            }
            else
            {
                for (int i = 0; i < vector.Length; i++)
                {
                    vector[i] /= norm;
                }
            }
        }

        public static double IndexToFreq(int i, int frame_size, int sample_rate)
        {
            return (double)i * sample_rate / frame_size;
        }

        public static int FreqToIndex(double freq, int frame_size, int sample_rate)
        {
            return (int)Math.Round(frame_size * freq / sample_rate);
        }

        public static double FreqToBark(double f)
        {
            double z = (26.81 * f) / (1960.0 + f) - 0.53;

            if (z < 2.0)
            {
                z = z + 0.15 * (2.0 - z);
            }
            else if (z > 20.1)
            {
                z = z + 0.22 * (z - 20.1);
            }

            return z;
        }
    }
}
