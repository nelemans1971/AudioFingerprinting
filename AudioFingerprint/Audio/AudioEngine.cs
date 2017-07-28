using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Un4seen.Bass;
using AudioFingerprint.FFT;
using AudioFingerprint.FFT.FFTW;
using System.IO;
using Un4seen.Bass.AddOn.Mix;
using SoundTouch;

namespace AudioFingerprint.Audio
{
    public class AudioEngine : IDisposable
    {
        private bool alreadyDisposed;
        private BassService bassService;
        private FFTWService fftService;
        private AudioNormalizer audioNormalizer;
        
        private LowpassFilters lowpassFilters = null;
        private double[] absEnergy = null;

        public AudioEngine()
        {
            alreadyDisposed = false;
            this.bassService = new BassService();
            if (IntPtr.Size == 4)
            {
                // 32 bits
                //fftService = new CachedFFTWService(new FFTWService86());
                fftService = new FFTWService86();
            }
            else
            {
                // 64bits
                //fftService = new CachedFFTWService(new FFTWService64());
                fftService = new FFTWService64();
            }
            this.audioNormalizer = new AudioNormalizer();
        }

        ~AudioEngine()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
            alreadyDisposed = true;
        }

        protected virtual void Dispose(bool isDisposing)
        {
            if (!alreadyDisposed)
            {
                if (bassService != null)
                {
                    bassService.Dispose();
                    bassService = null;
                }
                if (fftService != null)
                {
                    fftService.Dispose();
                    fftService = null;
                }
            }
        }


        public void Close()
        {
            Dispose(false);
        }

        /// <summary>
        /// Read audio file and mix it to mono, then resample it to "samplerate"
        /// </summary>
        /// <param name="filename">Name of the file</param>
        /// <param name="samplerate">Output sample rate, default 5512hz</param>
        /// <param name="toReadInMS">Milliseconds to read of or <= 0 to read everything</param>
        /// <param name="startmillisecond">Start position in millisecond, default 0 for begin</param>
        /// <returns>Array of samples</returns>
        /// <remarks>
        /// Seeking capabilities of Bass where not used because of the possible
        /// timing errors on different formats.
        /// </remarks>
        public AudioSamples ReadMonoFromFile(string filename, int outputSamplerate = 5512, int startPositionInMS = 0, int toReadInMS = -1)
        {
            if (!File.Exists(filename))
            {
                throw new Exception("File '" + filename + "' doesn't exists.");
            }

            // calculate total milliseconds to read
            int totalmilliseconds = toReadInMS <= 0 ? Int32.MaxValue : (toReadInMS + startPositionInMS);

            float[] data = null;

            //create streams for re-sampling
            int stream = CreateStream(filename);
            int mixerStream = CreateMixerStream(outputSamplerate);

            if (!bassService.CombineMixerStreams(mixerStream, stream, BASSFlag.BASS_MIXER_FILTER | BASSFlag.BASS_MIXER_DOWNMIX))
            {
                throw new BassException(bassService.GetLastError());
            }

            int bufferSize = outputSamplerate * 10 * 4; /*read 10 seconds at each iteration*/
            float[] buffer = new float[bufferSize];
            List<float[]> chunks = new List<float[]>();
            int size = 0;
            while ((float)(size) / outputSamplerate * 1000 < totalmilliseconds)
            {
                // get re-sampled/mono data
                int bytesRead = Bass.BASS_ChannelGetData(mixerStream, buffer, bufferSize);
                if (bytesRead == 0)
                {
                    break;
                }
                float[] chunk = new float[bytesRead / 4]; //each float contains 4 bytes
                Array.Copy(buffer, chunk, bytesRead / 4);
                chunks.Add(chunk);
                size += bytesRead / 4; //size of the data
            } //while

            if ((float)(size) / outputSamplerate * 1000 < (toReadInMS + startPositionInMS))
            {
                // not enough samples to return the requested data
                return null;
            }

            // Do bass cleanup
            Bass.BASS_ChannelStop(mixerStream);
            Bass.BASS_ChannelStop(stream);
            Bass.BASS_StreamFree(mixerStream);
            Bass.BASS_StreamFree(stream);

            int start = (int)((float)startPositionInMS * outputSamplerate / 1000);
            int end = (toReadInMS <= 0) ? size : (int)((float)(startPositionInMS + toReadInMS) * outputSamplerate / 1000);

            data = new float[size];
            int index = 0;
            // Concatenate
            foreach (float[] chunk in chunks)
            {
                Array.Copy(chunk, 0, data, index, chunk.Length);
                index += chunk.Length;
            }

            // Select specific part of the song
            if (start != 0 || end != size)
            {
                float[] temp = new float[end - start];
                Array.Copy(data, start, temp, 0, end - start);
                data = temp;
            }

            // Create audiosamples object
            AudioSamples audioSamples = new AudioSamples();
            audioSamples.Origin = filename;
            audioSamples.Channels = 1;
            audioSamples.SampleRate = outputSamplerate;
            audioSamples.StartInMS = start;
            audioSamples.DurationInMS = end;
            audioSamples.Samples = data;

            return audioSamples;
        }

        /// <summary>
        /// Resample to new samplerate and in mono
        /// </summary>
        public AudioSamples Resample(float[] inputAudioSamples, int inputSampleRate = 44100, int inputChannels = 2, int outputSamplerate = 5512)
        {
            //create streams for re-sampling
            int stream = Bass.BASS_StreamCreatePush(inputSampleRate, inputChannels, GetDefaultFlags(), IntPtr.Zero);
            ThrowIfStreamIsInvalid(stream);
            int mixerStream = BassMix.BASS_Mixer_StreamCreate(outputSamplerate, 1, GetDefaultFlags());
            ThrowIfStreamIsInvalid(mixerStream);
            if (!bassService.CombineMixerStreams(mixerStream, stream, BASSFlag.BASS_MIXER_FILTER | BASSFlag.BASS_MIXER_DOWNMIX))
            {
                throw new BassException(bassService.GetLastError());
            }
            Bass.BASS_StreamPutData(stream, inputAudioSamples, inputAudioSamples.Length * 4);

            int bufferSize = outputSamplerate * 10 * 4; /*read 10 seconds at each iteration*/
            float[] buffer = new float[bufferSize];
            List<float[]> chunks = new List<float[]>();
            int size = 0;

            int bytesRead;
            do
            {
                // get re-sampled/mono data
                bytesRead = Bass.BASS_ChannelGetData(mixerStream, buffer, bufferSize);
                if (bytesRead == 0)
                {
                    break;
                }
                float[] chunk = new float[bytesRead / 4]; //each float contains 4 bytes
                Array.Copy(buffer, chunk, bytesRead / 4);
                chunks.Add(chunk);
                size += bytesRead / 4; //size of the data
            } while (bytesRead > 0);


            // Do bass cleanup
            Bass.BASS_ChannelStop(mixerStream);
            Bass.BASS_ChannelStop(stream);
            Bass.BASS_StreamFree(mixerStream);
            Bass.BASS_StreamFree(stream);

            float[] data = new float[size];
            int index = 0;
            // Concatenate
            foreach (float[] chunk in chunks)
            {
                Array.Copy(chunk, 0, data, index, chunk.Length);
                index += chunk.Length;
            }

            // Create audiosamples object
            AudioSamples audioSamples = new AudioSamples();
            audioSamples.Origin = "MEMORY";
            audioSamples.Channels = 1;
            audioSamples.SampleRate = outputSamplerate;
            audioSamples.StartInMS = 0;
            audioSamples.DurationInMS = (int)(((float)size / (float)outputSamplerate) * 1000.0f);
            audioSamples.Samples = data;

            return audioSamples;
        }

        /// <summary>
        /// Stretches audio, keeps channels and samplerate.
        /// negatief value slowsdown (makes longer) the audio
        /// Positief value speedsup (make shorter) the audio
        /// </summary>
        public AudioSamples TimeStretch(string filename, float rateFactor)
        {
            if (!File.Exists(filename))
            {
                throw new Exception("File '" + filename + "' doesn't exists.");
            }

            // calculate total milliseconds to read
            int totalmilliseconds = Int32.MaxValue;

            float[] data = null;

            //create streams for re-sampling
            int stream = bassService.CreateStream(filename, BASSFlag.BASS_STREAM_DECODE | BASSFlag.BASS_SAMPLE_FLOAT);
            ThrowIfStreamIsInvalid(stream);
            BASS_CHANNELINFO channelInfo = Bass.BASS_ChannelGetInfo(stream);

            SoundTouch<Single, Double> soundTouch = new SoundTouch<Single, Double>();
            soundTouch.SetSampleRate(channelInfo.freq);
            soundTouch.SetChannels(channelInfo.chans);
            soundTouch.SetTempoChange(0.0f);
            soundTouch.SetPitchSemiTones(0.0f);
            soundTouch.SetRateChange(rateFactor); // -1.4f = Radio 538 setting
            soundTouch.SetSetting(SettingId.UseQuickseek, 0);
            soundTouch.SetSetting(SettingId.UseAntiAliasFilter, 0);

            int bufferSize = 2048;
            float[] buffer = new float[bufferSize];
            List<float[]> chunks = new List<float[]>();
            int size = 0;
            int nSamples = 0;
            while ((float)(size) / channelInfo.freq * 1000 < totalmilliseconds)
            {
                // get re-sampled data
                int bytesRead = Bass.BASS_ChannelGetData(stream, buffer, bufferSize);
                if (bytesRead <= 0)
                {
                    break;
                }
                nSamples = (bytesRead / 4) / channelInfo.chans;
                // Feed the samples into SoundTouch processor
                soundTouch.PutSamples(buffer, nSamples);

                // Read ready samples from SoundTouch processor & write them output file.
                // NOTES:
                // - 'receiveSamples' doesn't necessarily return any samples at all
                //   during some rounds!
                // - On the other hand, during some round 'receiveSamples' may have more
                //   ready samples than would fit into 'sampleBuffer', and for this reason 
                //   the 'receiveSamples' call is iterated for as many times as it
                //   outputs samples.
                do
                {
                    nSamples = soundTouch.ReceiveSamples(buffer, (bufferSize / channelInfo.chans));
                    if (nSamples > 0)
                    {
                        float[] chunk = new float[nSamples * channelInfo.chans];
                        Array.Copy(buffer, chunk, nSamples * channelInfo.chans);
                        chunks.Add(chunk);
                        size += nSamples * channelInfo.chans; //size of the data
                    }
                } while (nSamples != 0);
            } //while

            // Now the input file is processed, yet 'flush' few last samples that are
            // hiding in the SoundTouch's internal processing pipeline.
            soundTouch.Flush();
            do
            {
                nSamples = soundTouch.ReceiveSamples(buffer, (bufferSize / channelInfo.chans));
                if (nSamples > 0)
                {
                    float[] chunk = new float[nSamples * channelInfo.chans];
                    Array.Copy(buffer, chunk, nSamples * channelInfo.chans);
                    chunks.Add(chunk);
                    size += nSamples * channelInfo.chans; //size of the data
                }
            } while (nSamples != 0);


            if (size <= 0)
            {
                // not enough samples to return the requested data
                return null;
            }

            // Do bass cleanup
            Bass.BASS_ChannelStop(stream);
            Bass.BASS_StreamFree(stream);

            int start = 0;
            int end = size;

            data = new float[size];
            int index = 0;
            // Concatenate
            foreach (float[] chunk in chunks)
            {
                Array.Copy(chunk, 0, data, index, chunk.Length);
                index += chunk.Length;
            }

            // Select specific part of the song
            if (start != 0 || end != size)
            {
                float[] temp = new float[end - start];
                Array.Copy(data, start, temp, 0, end - start);
                data = temp;
            }

            // Create audiosamples object
            AudioSamples audioSamples = new AudioSamples();
            audioSamples.Origin = filename;
            audioSamples.Channels = channelInfo.chans;
            audioSamples.SampleRate = channelInfo.freq;
            audioSamples.StartInMS = start;
            audioSamples.DurationInMS = end;
            audioSamples.Samples = data;

            return audioSamples;
        }

        /// <summary>
        /// Stretches audio, keeps channels and samplerate.
        /// negatief value slowsdown (makes longer) the audio
        /// Positief value speedsup (make shorter) the audio
        /// </summary>
        public AudioSamples TimeStretch(float[] inputAudioSamples, int inputSampleRate = 44100, int inputChannels = 2, float rateFactor = 0.0f)
        {
            // calculate total milliseconds to read
            int totalmilliseconds = Int32.MaxValue;

            float[] data = null;

            int stream = Bass.BASS_StreamCreatePush(inputSampleRate, inputChannels, BASSFlag.BASS_STREAM_DECODE | BASSFlag.BASS_SAMPLE_FLOAT, IntPtr.Zero);
            ThrowIfStreamIsInvalid(stream);
            BASS_CHANNELINFO channelInfo = Bass.BASS_ChannelGetInfo(stream);
            Bass.BASS_StreamPutData(stream, inputAudioSamples, inputAudioSamples.Length * 4);

            SoundTouch<Single, Double> soundTouch = new SoundTouch<Single, Double>();
            soundTouch.SetSampleRate(channelInfo.freq);
            soundTouch.SetChannels(channelInfo.chans);
            soundTouch.SetTempoChange(0.0f);
            soundTouch.SetPitchSemiTones(0.0f);
            soundTouch.SetRateChange(rateFactor); // -1.4f = Radio 538 setting
            soundTouch.SetSetting(SettingId.UseQuickseek, 0);
            soundTouch.SetSetting(SettingId.UseAntiAliasFilter, 0);

            int bufferSize = 2048;
            float[] buffer = new float[bufferSize];
            List<float[]> chunks = new List<float[]>();
            int size = 0;
            int nSamples = 0;
            while ((float)(size) / channelInfo.freq * 1000 < totalmilliseconds)
            {
                // get re-sampled data
                int bytesRead = Bass.BASS_ChannelGetData(stream, buffer, bufferSize);
                if (bytesRead <= 0)
                {
                    break;
                }
                nSamples = (bytesRead / 4) / channelInfo.chans;
                // Feed the samples into SoundTouch processor
                soundTouch.PutSamples(buffer, nSamples);

                // Read ready samples from SoundTouch processor & write them output file.
                // NOTES:
                // - 'receiveSamples' doesn't necessarily return any samples at all
                //   during some rounds!
                // - On the other hand, during some round 'receiveSamples' may have more
                //   ready samples than would fit into 'sampleBuffer', and for this reason 
                //   the 'receiveSamples' call is iterated for as many times as it
                //   outputs samples.
                do
                {
                    nSamples = soundTouch.ReceiveSamples(buffer, (bufferSize / channelInfo.chans));
                    if (nSamples > 0)
                    {
                        float[] chunk = new float[nSamples * channelInfo.chans];
                        Array.Copy(buffer, chunk, nSamples * channelInfo.chans);
                        chunks.Add(chunk);
                        size += nSamples * channelInfo.chans; //size of the data
                    }
                } while (nSamples != 0);
            } //while

            // Now the input file is processed, yet 'flush' few last samples that are
            // hiding in the SoundTouch's internal processing pipeline.
            soundTouch.Flush();
            do
            {
                nSamples = soundTouch.ReceiveSamples(buffer, (bufferSize / channelInfo.chans));
                if (nSamples > 0)
                {
                    float[] chunk = new float[nSamples * channelInfo.chans];
                    Array.Copy(buffer, chunk, nSamples * channelInfo.chans);
                    chunks.Add(chunk);
                    size += nSamples * channelInfo.chans; //size of the data
                }
            } while (nSamples != 0);


            if (size <= 0)
            {
                // not enough samples to return the requested data
                return null;
            }

            // Do bass cleanup
            Bass.BASS_ChannelStop(stream);
            Bass.BASS_StreamFree(stream);

            int start = 0;
            int end = size;

            data = new float[size];
            int index = 0;
            // Concatenate
            foreach (float[] chunk in chunks)
            {
                Array.Copy(chunk, 0, data, index, chunk.Length);
                index += chunk.Length;
            }

            // Select specific part of the song
            if (start != 0 || end != size)
            {
                float[] temp = new float[end - start];
                Array.Copy(data, start, temp, 0, end - start);
                data = temp;
            }

            // Create audiosamples object
            AudioSamples audioSamples = new AudioSamples();
            audioSamples.Origin = "MEMORY";
            audioSamples.Channels = channelInfo.chans;
            audioSamples.SampleRate = channelInfo.freq;
            audioSamples.StartInMS = start;
            audioSamples.DurationInMS = end;
            audioSamples.Samples = data;

            return audioSamples;
        }

        public FingerprintSignature CreateFingerprint(AudioSamples audioSamples, SpectrogramConfig configuration)
        {
            absEnergy = new double[(Frequencies.Length - 1) * 4]; // number of frequencies bands (33)
            lowpassFilters = new LowpassFilters(Frequencies.Length - 1); // 33 bands
            try
            {
                int width = (audioSamples.Samples.Length - configuration.WdftSize) / configuration.Overlap;  // WdftSize=2048 / Overlap=64
                if (width < 1)
                {
                    return null;
                }

                // reserve memory for 32 bit fingerprint hashes
                byte[] hashes = new byte[width * sizeof(uint)];
                byte[] reliabilities = new byte[width * 32]; // elke subfinger (32 bits) heeft voor elke bit een waarde tussen 0 en 31 voor relibility (dus 5 bits), we ronden dit af naar 1 byte
                FingerprintSignature fingerprintSignature = new FingerprintSignature(null, 0, hashes, (long)(width * 11.6));
                fingerprintSignature.Reliabilities = reliabilities;

                // Calculate a hamming windows
                float[] hammingWindow = new float[configuration.WdftSize];
                for (int i = 0; i < hammingWindow.Length; i++)
                {
                    // Hamming (watch it peak is at beginning not as in real hamming in the middle)
                    hammingWindow[i] = 0.54f + 0.46f * (float)System.Math.Cos((6.283f * (float)i / hammingWindow.Length));
                    //hammingWindow[i] = 0.54f - (0.46f * (float)System.Math.Cos(((6.283f * (float)i) / (hammingWindow.Length - 1))));  // real hamming window
                } //for

                int[] frequenciesRange = Frequencies; // 34 freqencies                
                float[] samples = new float[configuration.WdftSize];

                for (int i = 0; i < width; i++)
                {
                    if (((samples.Length - 1) + (i * configuration.Overlap)) >= audioSamples.Samples.Length)
                    {
                        // we hebben niet voldoende data meer!
                        // dus we stoppen nu en nemen het "laaste" stukje niet mee!
                        break;
                    }

                    for (int j = 0; j < samples.Length; j++)
                    {
                        samples[j] = audioSamples.Samples[j + (i * configuration.Overlap)] * hammingWindow[j];
                    } // for j

                    float[] complexSignal = fftService.FFTForward(samples, 0, configuration.WdftSize);
                    byte[] reliability;
                    uint subFingerprint = CalculateSubFingerprint(complexSignal, frequenciesRange, 5512, out reliability);

                    Buffer.BlockCopy(BitConverter.GetBytes(subFingerprint), 0, hashes, i * sizeof(uint), sizeof(uint));
                    Buffer.BlockCopy(reliability, 0, reliabilities, i * reliability.Length, reliability.Length);
                    // sequencenumber = i;
                    // timestamp = (i / audioSamples.Samples.Length) * configuration.SampleRate
                } //for

                return fingerprintSignature;
            }
            finally
            {
                absEnergy = null;
                lowpassFilters = null;
            }
        }

        private uint CalculateSubFingerprint(float[] spectrum, int[] frequenciesRange, int sampleRate, out byte[] reliability)
        {
            reliability = new byte[32];
            Dictionary<byte, double> dReliability = new Dictionary<byte, double>(); // bitnummer, abs(energy)

            if (absEnergy == null || lowpassFilters == null)
            {
                throw new Exception("absEnergy or lowpassFilters is not initialized");

            }
            uint subFingerprint = 0;

            int samples = (spectrum.Length / 2); // 2048
            int numBands = frequenciesRange.Length - 1; // 33

            // 1. Calculate offsets frequencies in fft spectrum
            int[] xovr = new int[frequenciesRange.Length];
            for (int i = 0; i < frequenciesRange.Length; i++)
            {
                xovr[i] = (frequenciesRange[i] * samples / sampleRate) * 2; // "*2" because of Real and Imaginary part
            } // for i
            int samplesInFreqRange = (xovr[xovr.Length - 1] - xovr[0]) / 2;

            /*
            // Debug output of band energy
            string pattern = "-123456789";
            for (int b = 0; b < numBands; b++)
            {
                double e = 0;
                for (int i = xovr[b]; i < xovr[b + 1]; i += 2)
                {
                    e += (spectrum[i + 1] * spectrum[i + 1]) + (spectrum[i] * spectrum[i]);
                }
                e /= samplesInFreqRange;
                e = System.Math.Sqrt(e);
                if (e == 0)
                {
                    Console.Write(" ");
                }
                else if (e < 1)
                {
                    Console.Write(pattern[(int)(e * (pattern.Length - 1))]);
                }
                else
                {
                    Console.Write("!");
                }
            }
            Console.WriteLine();
            */
            
            // 2. calculate sub fingerprint
            // using purely absolute power based

            // rotate per band power history (needed for low pass filter and differentiation)
            Buffer.BlockCopy(absEnergy, 0, absEnergy, numBands * sizeof(double), numBands * 3 * sizeof(double));
            int width = spectrum.Length / 2;
            for (int b = 0; b < numBands; b++)
            {
                int lowBound = xovr[b];
                int higherBound = xovr[b + 1];
                absEnergy[b] = 0;

                for (int f = lowBound; f < higherBound; f += 2)
                {
                    double re = spectrum[f];
                    double img = spectrum[f + 1];
                    absEnergy[b] += (re * re) + (img * img);
                } //for k

                absEnergy[b] /= samplesInFreqRange;//                samples;
                absEnergy[b] = System.Math.Sqrt(absEnergy[b]);

                lowpassFilters.lowpass(absEnergy, b, Lowpass_Filter.FILTER_LOWPASS_IIR);

                if (b < (numBands - 1))
                {
                    double edge = absEnergy[b] - absEnergy[b + 1] - (absEnergy[numBands + b] - absEnergy[numBands + b + 1]);
                    subFingerprint <<= 1;
                    if (edge > 0)
                    {
                        subFingerprint++;
                    }
                    // bitnumber, reliability
                    dReliability.Add((byte)((numBands - 2) - b), System.Math.Abs(edge));
                }
            } //for i

            // set reliability for every bit order from 0..31
            byte r = 0;
            foreach (byte bitNumber in dReliability.OrderBy(e => e.Value).Select(e => e.Key).ToArray())
            {
                reliability[bitNumber] = r;
                r++;
            }

            return subFingerprint;
        }

        /// <summary>
        /// Bark-Frequenzen, van Bark 2.0 tot Bark 13.0 34 frequencies voor 33 banden
        /// </summary>
        private int[] Frequencies
        {
            get
            {
                return new int[34] { 204, 234, 265, 297, 330, 363, 399, 434, 471, 509, 
                                     548, 588, 631, 674, 718, 766, 813, 862, 915, 967, 
                                     1022, 1082, 1141, 1202, 1268, 1334, 1404, 1479, 1554, 1633, 
                                     1719, 1806, 1899, 1996
                                   };
            }
        }
        // ===================================================================================================================


        /// <summary>
        /// Creates bass stream, when error throws an exception
        /// </summary>
        private int CreateStream(string pathToFile)
        {
            int stream = bassService.CreateStream(pathToFile, GetDefaultFlags());
            ThrowIfStreamIsInvalid(stream);
            return stream;
        }

        private int CreateMixerStream(int sampleRate)
        {
            int stream = bassService.CreateMixerStream(sampleRate, 1, GetDefaultFlags());
            ThrowIfStreamIsInvalid(stream);
            return stream;
        }

        private BASSFlag GetDefaultFlags()
        {
            return BASSFlag.BASS_STREAM_DECODE | BASSFlag.BASS_SAMPLE_MONO | BASSFlag.BASS_SAMPLE_FLOAT;
        }

        private bool IsStreamValid(int stream)
        {
            return stream != 0;
        }

        private void ThrowIfStreamIsInvalid(int stream)
        {
            if (!IsStreamValid(stream))
            {
                throw new BassException(bassService.GetLastError());
            }
        }
    }
}
