using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AudioFingerprint.Audio
{
    public enum Lowpass_Filter 
        { 
            FILTER_LOWPASS_AVERAGE = 0,
            FILTER_LOWPASS_IIR
        }

    class LowpassFilters
    {
        private int numBands;
        private double[][] xv;
        private double[][] yv;

        public LowpassFilters(int freqencieBands)
        {
            numBands = freqencieBands;

            // for IIR lowpass filter
            xv = new double[numBands][];
            for (int i = 0; i < numBands; i++)
            {
                xv[i] = new double[5 + 1]; // NZEROS=5
            } //for i

            yv = new double[numBands][];
            for (int i = 0; i < numBands; i++)
            {
                yv[i] = new double[5 + 1]; // NPOLES=5
            } //for i
        }

        /// <summary>
        /// The following is a 12Hz Butterworth 5th order lowpass code-generated using http://www-users.cs.york.ac.uk/~fisher/cgi-bin/mkfscript
        /// </summary>
        private void lowpass_iir(double[] values, int band)
        {
            xv[band][0] = xv[band][1];
            xv[band][1] = xv[band][2];
            xv[band][2] = xv[band][3];
            xv[band][3] = xv[band][4];
            xv[band][4] = xv[band][5];
            xv[band][5] = values[band];    // deze lijkt me beter
            //xv[band][4] = values[band];      // Lijkt me fout!

            yv[band][0] = yv[band][1];
            yv[band][1] = yv[band][2];
            yv[band][2] = yv[band][3];
            yv[band][3] = yv[band][4];
            yv[band][4] = yv[band][5];

            yv[band][5] = (xv[band][0] + xv[band][5]) + 5 * (xv[band][1] + xv[band][4]) + 10 * (xv[band][2] + xv[band][3])
                             + (0.1531844342 * yv[band][0]) + (-1.0474572756 * yv[band][1])
                             + (2.9333140266 * yv[band][2]) + (-4.2282741408 * yv[band][3])
                             + (3.1622098654 * yv[band][4]);


            values[band] = yv[band][5];
        }

        /// <summary>
        /// calculate average to realize low-pass
        /// </summary>
        private void lowpass_average(double[] values, int band)
        {
            values[band] += values[band + numBands];
            values[band] += values[band + numBands * 2];
            values[band] += values[band + numBands * 3];
            values[band] /= 4;
        }

        public void lowpass(double[] values, int band, Lowpass_Filter filterType = Lowpass_Filter.FILTER_LOWPASS_IIR)
        {
            switch (filterType)
            {
                case Lowpass_Filter.FILTER_LOWPASS_AVERAGE:
                    lowpass_average(values, band);
                    break;
                case Lowpass_Filter.FILTER_LOWPASS_IIR:
                default:
                    lowpass_iir(values, band);
                    break;
            } //switch
        }

    }
}
