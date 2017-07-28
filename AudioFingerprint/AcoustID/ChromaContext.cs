// -----------------------------------------------------------------------
// <copyright file="ChromaContext.cs" company="">
// Original C++ implementation by Lukas Lalinsky, http://acoustid.org/chromaprint
// </copyright>
// -----------------------------------------------------------------------

namespace AcoustID
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using AcoustID.Chromaprint;
    using AcoustID.Util;

    /// <summary>
    /// The main Chromaprint API.
    /// </summary>
    public class ChromaContext
    {
        /// <summary>
        /// Return the version number of Chromaprint.
        /// </summary>
        public static readonly string Version = "1.1.0";

        Fingerprinter fingerprinter;
        int algorithm;

        int[] fingerprint;

        /// <summary>
        /// Gets the fingerprint algorithm this context is configured to use.
        /// </summary>
        public int Algorithm
        {
            get { return algorithm; }
        }

        /// <summary>
        /// Gets the <see cref="IAudioConsumer"/> which consumes the decoded audio.
        /// </summary>
        public IAudioConsumer Consumer
        {
            get { return fingerprinter; }
        }

        public ChromaContext()
            : this(ChromaprintAlgorithm.TEST2)
        {
        }

        public ChromaContext(int algorithm)
        {
            this.algorithm = algorithm;

            var config = FingerprinterConfiguration.CreateConfiguration(algorithm);
            this.fingerprinter = new Fingerprinter(config);
        }

        /// <summary>
        /// Set a configuration option for the selected fingerprint algorithm.
        ///
        /// NOTE: DO NOT USE THIS FUNCTION IF YOU ARE PLANNING TO USE
        /// THE GENERATED FINGERPRINTS WITH THE ACOUSTID SERVICE.
        /// 
        /// Possible options:
        ///  - silence_threshold: threshold for detecting silence, 0-32767
        /// </summary>
        /// <param name="name">option name</param>
        /// <param name="value">option value</param>
        /// <returns>False on error, true on success</returns>
        public bool SetOption(string name, int value)
        {
            return fingerprinter.SetOption(name, value);
        }

        /// <summary>
        /// Restart the computation of a fingerprint with a new audio stream
        /// </summary>
        /// <param name="sample_rate">sample rate of the audio stream (in Hz)</param>
        /// <param name="num_channels">numbers of channels in the audio stream (1 or 2)</param>
        /// <returns>False on error, true on success</returns>
        public bool Start(int sample_rate, int num_channels)
        {
            return fingerprinter.Start(sample_rate, num_channels);
        }

        /// <summary>
        /// Send audio data to the fingerprint calculator.
        /// </summary>
        /// <param name="data">raw audio data, should point to an array of 16-bit 
        /// signed integers in native byte-order</param>
        /// <param name="size">size of the data buffer (in samples)</param>
        public void Feed(short[] data, int size)
        {
            fingerprinter.Consume(data, size);
        }

        /// <summary>
        /// Process any remaining buffered audio data and calculate the fingerprint.
        /// </summary>
        public bool Finish()
        {
            fingerprint = fingerprinter.Finish();
            return (this.fingerprint != null);
        }

        /// <summary>
        /// Return the calculated fingerprint as a compressed string.
        /// </summary>
        /// <returns>The fingerprint as a compressed string</returns>
        public string GetFingerprint()
        {
            FingerprintCompressor compressor = new FingerprintCompressor();
            return Base64.Encode(compressor.Compress(this.fingerprint, algorithm));
        }

        /// <summary>
        /// Return the calculated fingerprint as an array of 32-bit integers.
        /// </summary>
        /// <returns>The raw fingerprint (array of 32-bit integers)</returns>
        public int[] GetRawFingerprint()
        {
            int size = this.fingerprint.Length;
            int[] fp = new int[size];

            // TODO: copying necessary?
            Array.Copy(this.fingerprint, fp, size);
            return fp;
        }

        /// <summary>
        /// Compress and optionally base64-encode a raw fingerprint.
        /// </summary>
        /// <param name="fp">pointer to an array of 32-bit integers representing the raw fingerprint to be encoded</param>
        /// <param name="algorithm">Chromaprint algorithm version which was used to generate the raw fingerprint</param>
        /// <param name="base64">Whether to return binary data or base64-encoded ASCII data. The
        /// compressed fingerprint will be encoded using base64 with the URL-safe scheme if you 
        /// set this parameter to 1. It will return binary data if it's 0.</param>
        /// <returns>The encoded fingerprint</returns>
        public static byte[] EncodeFingerprint(int[] fp, int algorithm, bool base64)
        {
            FingerprintCompressor compressor = new FingerprintCompressor();
            string compressed = compressor.Compress(fp, algorithm);

            if (!base64)
            {
                return Base64.ByteEncoding.GetBytes(compressed);
            }

            return Base64.ByteEncoding.GetBytes(Base64.Encode(compressed));
        }

        /// <summary>
        /// Uncompress and optionally base64-decode an encoded fingerprint.
        /// </summary>
        /// <param name="encoded_fp">Pointer to an encoded fingerprint</param>
        /// <param name="base64">Whether the encoded_fp parameter contains binary data or base64-encoded
        /// ASCII data. If 1, it will base64-decode the data before uncompressing the fingerprint.</param>
        /// <param name="algorithm">Chromaprint algorithm version which was used to generate the raw 
        /// fingerprint</param>
        /// <returns>The decoded raw fingerprint (array of 32-bit integers)</returns>
        public static int[] DecodeFingerprint(byte[] encoded_fp, bool base64, out int algorithm)
        {
            string encoded = Base64.ByteEncoding.GetString(encoded_fp);
            string compressed = base64 ? Base64.Decode(encoded) : encoded;

            algorithm = 0;
            
            FingerprintDecompressor decompressor = new FingerprintDecompressor();
            int[] uncompressed = decompressor.Decompress(compressed, ref algorithm);

            int size = uncompressed.Length;
            int[] fp = new int[size];
            // TODO: copying necessary?
            Array.Copy(uncompressed, fp, size);

            return fp;
        }
    }
}
