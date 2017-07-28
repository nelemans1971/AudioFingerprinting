// -----------------------------------------------------------------------
// <copyright file="Lloyds.cs" company="">
// Original C++ implementation by Lukas Lalinsky, http://acoustid.org/chromaprint
// </copyright>
// -----------------------------------------------------------------------

namespace AcoustID.Chromaprint
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// TODO: Update summary.
    /// </summary>
    public static class Lloyds
    {
        public static double[] Compute(List<double> sig, int len)
        {
            double[] x = new double[len - 1];
            double[] q = new double[len];

            sig.Sort();

            // Set initial endpoints
            double sig_min = sig[0];
            double sig_max = sig[sig.Count - 1];

            // Initial parameters
            for (int i = 0; i < len; i++)
            {
                q[i] = i * (sig_max - sig_min) / (len - 1) + sig_min;
            }
            for (int i = 0; i < len - 1; i++)
            {
                x[i] = (q[i] + q[i + 1]) / 2;
            }

            double reldist = 1.0, dist = 1.0;
            double stop_criteria = Math.Max(double.Epsilon * Math.Abs(sig_max), 1e-7);
            double iteration = 0;
            while (reldist > stop_criteria)
            {
                iteration++;
                reldist = dist;
                dist = 0.0;

                int sig_it = 0;
                for (int i = 0; i < len; i++)
                {
                    double sum = 0.0;
                    int cnt = 0;
                    while (sig_it < sig.Count && (i == len - 1 || sig[sig_it] < x[i]))
                    {
                        sum += sig[sig_it];
                        dist += (sig[sig_it] - q[i]) * (sig[sig_it] - q[i]);
                        ++cnt;
                        ++sig_it;
                    }
                    if (cnt > 0)
                    {
                        q[i] = sum / cnt;
                    }
                    else if (i == 0)
                    {
                        q[i] = (sig_min + x[i]) / 2.0;
                    }
                    else if (i == len - 1)
                    {
                        q[i] = (x[i - 1] + sig_max) / 2.0;
                    }
                    else
                    {
                        q[i] = (x[i - 1] + x[i]) / 2.0;
                    }
                }

                dist /= sig.Count;
                reldist = Math.Abs(reldist - dist);

                // Set the endpoints in between the updated quanta
                for (int i = 0; i < len - 1; i++)
                {
                    x[i] = (q[i] + q[i + 1]) / 2.0;
                }
            }

            return x;
        }
    }
}
