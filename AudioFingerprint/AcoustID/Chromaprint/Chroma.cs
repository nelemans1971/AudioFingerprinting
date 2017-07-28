// -----------------------------------------------------------------------
// <copyright file="Chroma.cs" company="">
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
    /// Transform frequencies into musical notes.
    /// </summary>
    internal class Chroma : IFFTFrameConsumer
    {
        static readonly int NUM_BANDS = 12;

        bool m_interpolate;
        byte[] m_notes;
        double[] m_notes_frac;
        int m_min_index;
        int m_max_index;
        double[] m_features;
        IFeatureVectorConsumer m_consumer;

        public bool Interpolate
        {
            get { return m_interpolate; }
            set { m_interpolate = value; }
        }

        public Chroma(int min_freq, int max_freq, int frame_size, int sample_rate, IFeatureVectorConsumer consumer)
        {
            m_interpolate = false;
            m_notes = new byte[frame_size];
            m_notes_frac = new double[frame_size];
            m_features = new double[NUM_BANDS];
            m_consumer = consumer;

            PrepareNotes(min_freq, max_freq, frame_size, sample_rate);
        }

        private double FreqToOctave(double freq, double _base = 440.0 / 16.0)
        {
            return Math.Log(freq / _base) / Math.Log(2.0);
        }

        private void PrepareNotes(int min_freq, int max_freq, int frame_size, int sample_rate)
        {
            m_min_index = Math.Max(1, Helper.FreqToIndex(min_freq, frame_size, sample_rate));
            m_max_index = Math.Min(frame_size / 2, Helper.FreqToIndex(max_freq, frame_size, sample_rate));
            for (int i = m_min_index; i < m_max_index; i++)
            {
                double freq = Helper.IndexToFreq(i, frame_size, sample_rate);
                double octave = FreqToOctave(freq);
                double note = NUM_BANDS * (octave - Math.Floor(octave));
                m_notes[i] = (byte)note;
                m_notes_frac[i] = note - m_notes[i];
            }
        }

        public void Reset()
        {
        }

        public void Consume(FFTFrame frame)
        {
            // TODO: do we really need to create a new instance here
            m_features = new double[NUM_BANDS];

            // Yes, we do. See ChromaFilter: m_buffer[i][] would reference
            // the same array for all i.

            //for (int i = 0; i < m_features.Length; i++)
            //{
            //    m_features[i] = 0.0;
            //}

            for (int i = m_min_index; i < m_max_index; i++)
            {
                int note = m_notes[i];
                double energy = frame.Energy(i);
                if (m_interpolate)
                {
                    int note2 = note;
                    double a = 1.0;
                    if (m_notes_frac[i] < 0.5)
                    {
                        note2 = (note + NUM_BANDS - 1) % NUM_BANDS;
                        a = 0.5 + m_notes_frac[i];
                    }
                    if (m_notes_frac[i] > 0.5)
                    {
                        note2 = (note + 1) % NUM_BANDS;
                        a = 1.5 - m_notes_frac[i];
                    }
                    m_features[note] += energy * a;
                    m_features[note2] += energy * (1.0 - a);
                }
                else
                {
                    m_features[note] += energy;
                }
            }
            m_consumer.Consume(m_features);
        }
    }
}
