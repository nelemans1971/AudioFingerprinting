// -----------------------------------------------------------------------
// <copyright file="Resampler.cs" company="">
// Original C++ implementation by Lukas Lalinsky, http://acoustid.org/chromaprint
// </copyright>
// -----------------------------------------------------------------------

namespace AcoustID.Audio
{
    using System;

    /// <summary>
    /// Audio resampling as implemented in FFmpeg.
    /// </summary>
    public class Resampler
    {
        struct ResampleContext
        {
            public short[] filter_bank;
            public int filter_length;
            public int ideal_dst_incr;
            public int dst_incr;
            public int index;
            public int frac;
            public int src_incr;
            public int compensation_distance;
            public int phase_shift;
            public int phase_mask;
            public bool linear;
        }

        static int FILTER_SHIFT = 15;
        static int WINDOW_TYPE = 9;

        ResampleContext ctx;

        /// <summary>
        /// Initialize the audio resampler. 
        /// </summary>
        /// <param name="out_rate">Output sample rate</param>
        /// <param name="in_rate">Input sample rate</param>
        /// <param name="filter_size">Length of each FIR filter in the filterbank relative to the 
        /// cutoff freq</param>
        /// <param name="phase_shift">Log2 of the number of entries in the polyphase filterbank</param>
        /// <param name="linear">If true then the used FIR filter will be linearly interpolated between 
        /// the 2 closest, if false the closest will be used</param>
        /// <param name="cutoff">Cutoff frequency, 1.0 corresponds to half the output sampling rate</param>
        public void Init(int out_rate, int in_rate, int filter_size, int phase_shift,
            bool linear, double cutoff)
        {
            ctx = default(ResampleContext);
            double factor = Min(out_rate * cutoff / in_rate, 1.0);
            int phase_count = 1 << phase_shift;

            //if (!c)
            //    return NULL;

            ctx.phase_shift = phase_shift;
            ctx.phase_mask = phase_count - 1;
            ctx.linear = linear;

            ctx.filter_length = (int)Max((int)Math.Ceiling(filter_size / factor), 1);
            ctx.filter_bank = new short[ctx.filter_length * (phase_count + 1)];
            //if (!c.filter_bank)
            //    goto error;
            BuildFilter(ctx.filter_bank, factor, ctx.filter_length, phase_count, 1 << FILTER_SHIFT, WINDOW_TYPE);
            //    goto error;
            Array.Copy(ctx.filter_bank, 0, ctx.filter_bank, ctx.filter_length * phase_count + 1, ctx.filter_length - 1);
            //memcpy(c.filter_bank[c.filter_length * phase_count + 1], c.filter_bank, (c.filter_length - 1) * sizeof(FELEM));
            ctx.filter_bank[ctx.filter_length * phase_count] = ctx.filter_bank[ctx.filter_length - 1];

            ctx.src_incr = out_rate;
            ctx.ideal_dst_incr = ctx.dst_incr = in_rate * phase_count;
            ctx.index = -phase_count * ((ctx.filter_length - 1) / 2);
        }

        /// <summary>
        /// Resample an array of samples using a previously configured context.
        /// </summary>
        /// <param name="dst"></param>
        /// <param name="src">Array of unconsumed samples </param>
        /// <param name="consumed">Number of samples of src which have been consumed are returned here</param>
        /// <param name="src_size">Number of unconsumed samples available </param>
        /// <param name="dst_size">Amount of space in samples available in dst</param>
        /// <param name="update_ctx">If this is false then the context will not be modified, that way several 
        /// channels can be resampled with the same context. </param>
        /// <returns>Number of samples written in dst or -1 if an error occurred</returns>
        public int Resample(short[] dst, short[] src, ref int consumed, int src_size, int dst_size, bool update_ctx)
        {
            int dst_index, i;
            int index = ctx.index;
            int frac = ctx.frac;
            int dst_incr_frac = ctx.dst_incr % ctx.src_incr;
            int dst_incr = ctx.dst_incr / ctx.src_incr;
            int compensation_distance = ctx.compensation_distance;

            if (compensation_distance == 0 && ctx.filter_length == 1 && ctx.phase_shift == 0)
            {
                long index2 = ((long)index) << 32;
                long incr = (1L << 32) * ctx.dst_incr / ctx.src_incr;
                dst_size = (int)Math.Min(dst_size, (src_size - 1 - index) * (long)ctx.src_incr / ctx.dst_incr);

                for (dst_index = 0; dst_index < dst_size; dst_index++)
                {
                    dst[dst_index] = src[index2 >> 32];
                    index2 += incr;
                }
                frac += dst_index * dst_incr_frac;
                index += dst_index * dst_incr;
                index += frac / ctx.src_incr;
                frac %= ctx.src_incr;
            }
            else
            {
                for (dst_index = 0; dst_index < dst_size; dst_index++)
                {
                    short[] filter = ctx.filter_bank;
                    int filter_offset = ctx.filter_length * (index & ctx.phase_mask);

                    int sample_index = index >> ctx.phase_shift;
                    int val = 0;

                    if (sample_index < 0)
                    {
                        for (i = 0; i < ctx.filter_length; i++)
                            val += src[Abs(sample_index + i) % src_size] * filter[filter_offset + i];
                    }
                    else if (sample_index + ctx.filter_length > src_size)
                    {
                        break;
                    }
                    else if (ctx.linear)
                    {
                        int v2 = 0;
                        for (i = 0; i < ctx.filter_length; i++)
                        {
                            val += src[sample_index + i] * (int)filter[filter_offset + i];
                            v2 += src[sample_index + i] * (int)filter[filter_offset + i + ctx.filter_length];
                        }
                        val += (int)((v2 - val) * (long)frac / ctx.src_incr);
                    }
                    else
                    {
                        for (i = 0; i < ctx.filter_length; i++)
                        {
                            val += src[sample_index + i] * (int)filter[filter_offset + i];
                        }
                    }

                    val = (val + (1 << (FILTER_SHIFT - 1))) >> FILTER_SHIFT;
                    dst[dst_index] = (short)((uint)(val + 32768) > 65535 ? (val >> 31) ^ 32767 : val);

                    frac += dst_incr_frac;
                    index += dst_incr;
                    if (frac >= ctx.src_incr)
                    {
                        frac -= ctx.src_incr;
                        index++;
                    }

                    if (dst_index + 1 == compensation_distance)
                    {
                        compensation_distance = 0;
                        dst_incr_frac = ctx.ideal_dst_incr % ctx.src_incr;
                        dst_incr = ctx.ideal_dst_incr / ctx.src_incr;
                    }
                }
            }
            consumed = Math.Max(index, 0) >> ctx.phase_shift;
            if (index >= 0) index &= ctx.phase_mask;

            if (compensation_distance != 0)
            {
                compensation_distance -= dst_index;
                // TODO: assert(compensation_distance > 0);
            }
            if (update_ctx)
            {
                ctx.frac = frac;
                ctx.index = index;
                ctx.dst_incr = dst_incr_frac + ctx.src_incr * dst_incr;
                ctx.compensation_distance = compensation_distance;
            }

            return dst_index;
        }

        public void Close()
        {
            ctx.filter_bank = null;
        }

        private void Compensate(int sample_delta, int compensation_distance)
        {
            ctx.compensation_distance = compensation_distance;
            ctx.dst_incr = ctx.ideal_dst_incr - (int)(ctx.ideal_dst_incr * (long)sample_delta / compensation_distance);
        }

        static int BuildFilter(short[] filter, double factor, int tap_count, int phase_count, int scale, int type)
        {
            int ph, i;
            double x, y, w;
            double[] tab = new double[tap_count];
            int center = (tap_count - 1) / 2;

            //if (!tab)
            //    return AVERROR(ENOMEM);

            // if upsampling, only need to interpolate, no filter
            if (factor > 1.0)
                factor = 1.0;

            for (ph = 0; ph < phase_count; ph++)
            {
                double norm = 0;
                for (i = 0; i < tap_count; i++)
                {
                    x = Math.PI * ((double)(i - center) - (double)ph / phase_count) * factor;
                    if (x == 0) y = 1.0;
                    else y = Math.Sin(x) / x;
                    switch (type)
                    {
                        case 0:
                            float d = -0.5f; //first order derivative = -0.5
                            x = Math.Abs(((double)(i - center) - (double)ph / phase_count) * factor);
                            if (x < 1.0) y = 1 - 3 * x * x + 2 * x * x * x + d * (-x * x + x * x * x);
                            else y = d * (-4 + 8 * x - 5 * x * x + x * x * x);
                            break;
                        case 1:
                            w = 2.0 * x / (factor * tap_count) + Math.PI;
                            y *= 0.3635819 - 0.4891775 * Math.Cos(w) + 0.1365995 * Math.Cos(2 * w) - 0.0106411 * Math.Cos(3 * w);
                            break;
                        default:
                            w = 2.0 * x / (factor * tap_count * Math.PI);
                            y *= Bessel(type * Math.Sqrt(Max(1 - w * w, 0)));
                            break;
                    }

                    tab[i] = y;
                    norm += y;
                }

                // normalize so that an uniform color remains the same
                for (i = 0; i < tap_count; i++)
                {
                    filter[ph * tap_count + i] = Clip(Floor(tab[i] * scale / norm), short.MinValue, short.MaxValue);
                }
            }

            //av_free(tab);
            return 0;
        }

        #region Math helper

        static int Abs(int a)
        {
            return ((a) >= 0 ? (a) : (-(a)));
        }

        static int Sign(int a)
        {
            return ((a) > 0 ? 1 : -1);
        }

        static double Max(double a, double b)
        {
            return ((a) > (b) ? (a) : (b));
        }

        static double Min(double a, double b)
        {
            return ((a) > (b) ? (b) : (a));
        }

        static int Floor(double x)
        {
            return (int)Math.Floor(x + 0.5);
        }

        static short Clip(int a, short amin, short amax)
        {
            if (a < amin) return amin;
            else if (a > amax) return amax;
            else return (short)a; // TODO: casting to short ok?
        }

        /// <summary>
        /// 0th order modified bessel function of the first kind.
        /// </summary>
        /// <param name="x"></param>
        /// <returns></returns>
        static double Bessel(double x)
        {
            double v = 1;
            double lastv = 0;
            double t = 1;
            int i;

            x = x * x / 4;
            for (i = 1; v != lastv; i++)
            {
                lastv = v;
                t *= x / (i * i);
                v += t;
            }
            return v;
        }

        #endregion
    }
}
