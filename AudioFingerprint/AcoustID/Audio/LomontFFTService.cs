// -----------------------------------------------------------------------
// <copyright file="LomontFFTService.cs" company="">
// Original code by Chris Lomont, 2010-2012, http://www.lomont.org/Software/
// </copyright>
// -----------------------------------------------------------------------

namespace AcoustID.Audio
{
    using System;
    using AcoustID.Util;

    /// <summary>
    /// FFT implementation by Chris Lomont (http://www.lomont.org/Software/).
    /// </summary>
    internal class LomontFFTService : IFFTService
    {
        double[] m_window;
        int m_frame_size;
        int m_frame_size_h;
        double[] m_input;

        public void Initialize(int frame_size, double[] window)
        {
            m_frame_size = frame_size;
            m_frame_size_h = frame_size / 2;
            m_window = window;

            m_input = new double[frame_size];

            this.ComputeTable(frame_size);
        }

        public void ComputeFrame(short[] input, double[] output)
        {
            for (int i = 0; i < m_frame_size; i++)
            {
                m_input[i] = (double)input[i];
            }

            Helper.ApplyWindow(ref m_input, m_window, m_frame_size, 1.0);

            this.RealFFT(m_input);

            // m_input will now contain the FFT values
            // r0, r(n/2), r1, i1, r2, i2 ...

            // Compute energy
            output[0] = m_input[0] * m_input[0];
            output[m_frame_size_h] = m_input[1] * m_input[1];

            for (int i = 1; i < m_frame_size_h; i++)
            {
                output[i] = m_input[2 * i] * m_input[2 * i] + m_input[2 * i + 1] * m_input[2 * i + 1];
            }
        }

        #region Lomont FFT

        // store sin and cos tables here
        double[] forwardCos;
        double[] forwardSin;

        /// <summary>
        /// Call this with the size before using the FFT
        /// Fills in tables for speed
        /// </summary>
        /// <param name="size">The table size.</param>
        public void ComputeTable(int size)
        {
            forwardCos = new double[size];
            forwardSin = new double[size];

            // forward pass
            int i = 0;
            int n = size;
            int mmax = 1;
            while (n > mmax)
            {
                int istep = 2 * mmax;
                double theta = Math.PI / mmax;
                double wr = 1, wi = 0;
                double wpr = Math.Cos(theta);
                double wpi = Math.Sin(theta);
                for (int m = 0; m < istep; m += 2)
                {
                    forwardCos[i] = wr;
                    forwardSin[i] = wi;
                    i++;

                    double t = wr;
                    wr = wr * wpr - wi * wpi;
                    wi = wi * wpr + t * wpi;
                }
                mmax = istep;
            }
        }

        /// <summary>
        /// Compute the forward or inverse FFT of data, which is 
        /// complex valued items, stored in alternating real and 
        /// imaginary real numbers. The length must be a power of 2.
        /// </summary>
        /// <param name="data">The audio data (time domain).</param>
        public void FFT(double[] data)
        {
            int n = data.Length;
            // check all are valid
            if ((n & (n - 1)) != 0) // checks n is a power of 2 in 2's complement format
                throw new Exception("data length " + n + " in FFT is not a power of 2");
            n /= 2;

            // bit reverse the indices. This is exercise 5 in section 7.2.1.1 of Knuth's TAOCP
            // the idea is a binary counter in k and one with bits reversed in j
            // see also Alan H. Karp, "Bit Reversals on Uniprocessors", SIAM Review, vol. 38, #1, 1--26, March (1996) 
            // nn = number of samples, 2* this is length of data?
            int j = 0, k = 0; // Knuth R1: initialize
            int top = n / 2; // this is Knuth's 2^(n-1)
            while (true)
            {
                // Knuth R2: swap
                // swap j+1 and k+2^(n-1) - both have two entries
                double t;
                t = data[j + 2]; data[j + 2] = data[k + n]; data[k + n] = t;
                t = data[j + 3]; data[j + 3] = data[k + n + 1]; data[k + n + 1] = t;
                if (j > k)
                { // swap two more
                    // j and k
                    t = data[j]; data[j] = data[k]; data[k] = t;
                    t = data[j + 1]; data[j + 1] = data[k + 1]; data[k + 1] = t;
                    // j + top + 1 and k+top + 1
                    t = data[j + n + 2]; data[j + n + 2] = data[k + n + 2]; data[k + n + 2] = t;
                    t = data[j + n + 3]; data[j + n + 3] = data[k + n + 3]; data[k + n + 3] = t;
                }
                // Knuth R3: advance k
                k += 4;
                if (k >= n)
                    break;
                // Knuth R4: advance j
                int h = top;
                while (j >= h)
                {
                    j -= h;
                    h /= 2;
                }
                j += h;
            } // bit reverse loop

            // do transform by doing single point transforms, then doubles, fours, etc.
            int mmax = 1;
            int tptr = 0;
            while (n > mmax)
            {
                int istep = 2 * mmax;
                double theta = Math.PI / mmax;
                for (int m = 0; m < istep; m += 2)
                {
                    double wr = forwardCos[tptr];
                    double wi = forwardSin[tptr++];
                    for (k = m; k < 2 * n; k += 2 * istep)
                    {
                        j = k + istep;
                        double tempr = wr * data[j] - wi * data[j + 1];
                        double tempi = wi * data[j] + wr * data[j + 1];
                        data[j] = data[k] - tempr;
                        data[j + 1] = data[k + 1] - tempi;
                        data[k] = data[k] + tempr;
                        data[k + 1] = data[k + 1] + tempi;
                    }
                }
                mmax = istep;
            }
        }

        /// <summary>
        /// Computes the real FFT.
        /// </summary>
        /// <param name="data">The audio data (time domain).</param>
        public void RealFFT(double[] data)
        {
            FFT(data); // do packed FFT

            int n = data.Length; // number of real input points, which is 1/2 the complex length
            double temp;

            double theta = 2 * Math.PI / n;
            double wpr = Math.Cos(theta);
            double wpi = Math.Sin(theta);
            double wjr = wpr;
            double wji = wpi;
            for (int j = 1; j <= n / 4; ++j)
            {
                int k = n / 2 - j;
                double tnr = data[2 * k];
                double tni = data[2 * k + 1];
                double tjr = data[2 * j];
                double tji = data[2 * j + 1];

                double e = (tjr + tnr);
                double f = (tji - tni);
                double a = (tjr - tnr) * wji;
                double d = (tji + tni) * wji;
                double b = (tji + tni) * wjr;
                double c = (tjr - tnr) * wjr;

                // compute entry y[j]
                data[2 * j] = 0.5 * (e + (a + b));
                data[2 * j + 1] = 0.5 * (f - (c - d));

                // compute entry y[k]
                data[2 * k] = 0.5 * (e - (a + b));
                data[2 * k + 1] = 0.5 * ((-c + d) - f);

                temp = wjr;
                wjr = wjr * wpr - wji * wpi;
                wji = temp * wpi + wji * wpr;
            }

            // compute final y0 and y_{N/2} ones, place into data[0] and data[1]
            temp = data[0];
            data[0] += data[1];
            data[1] = temp - data[1];
        }

        #endregion
    }
}
