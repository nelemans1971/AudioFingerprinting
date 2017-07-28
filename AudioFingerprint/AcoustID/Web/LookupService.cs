// -----------------------------------------------------------------------
// <copyright file="LookupService.cs" company="">
// Christian Woltering, https://github.com/wo80
// </copyright>
// -----------------------------------------------------------------------

namespace AcoustID.Web
{
    using System;
    using System.IO;
    using System.IO.Compression;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// Calls the AcoustID webservice to lookup audio data for a given fingerprint.
    /// </summary>
    public class LookupService
    {
        private const string URL = "http://api.acoustid.org/v2/lookup";

        private IResponseParser parser;

        public LookupService()
            : this(new XmlResponseParser())
        {
        }

        public LookupService(IResponseParser parser)
        {
            this.parser = parser;

            UseCompression = true;
        }

        /// <summary>
        /// Gets or sets a value indicating whether to compress the data before submit.
        /// </summary>
        public bool UseCompression { get; set; }

        /// <summary>
        /// Calls the webservice on a worker thread.
        /// </summary>
        /// <param name="fingerprint">The audio fingerprint.</param>
        /// <param name="duration">The total duration of the audio.</param>
        /// <returns>A task which returns a <see cref="LookupResponse"/>.</returns>
        public Task<LookupResponse> GetAsync(string fingerprint, int duration)
        {
            return GetAsync(fingerprint, duration, null);
        }

        /// <summary>
        /// Calls the webservice on a worker thread.
        /// </summary>
        /// <param name="fingerprint">The audio fingerprint.</param>
        /// <param name="duration">The total duration of the audio.</param>
        /// <param name="meta">Request meta information.</param>
        /// <returns>A task which returns a <see cref="LookupResponse"/>.</returns>
        public Task<LookupResponse> GetAsync(string fingerprint, int duration, string[] meta)
        {
            return Task.Factory.StartNew<LookupResponse>(() =>
            {
                return Get(fingerprint, duration, meta);
            });
        }

        /// <summary>
        /// Calls the webservice.
        /// </summary>
        /// <param name="fingerprint">The audio fingerprint.</param>
        /// <param name="duration">The total duration of the audio.</param>
        /// <returns>A <see cref="LookupResponse"/>.</returns>
        public LookupResponse Get(string fingerprint, int duration)
        {
            return Get(fingerprint, duration, null);
        }

        /// <summary>
        /// Calls the webservice.
        /// </summary>
        /// <param name="fingerprint">The audio fingerprint.</param>
        /// <param name="duration">The total duration of the audio.</param>
        /// <param name="meta">Request meta information.</param>
        /// <returns>A <see cref="LookupResponse"/>.</returns>
        public LookupResponse Get(string fingerprint, int duration, string[] meta)
        {
            try
            {
                string request = BuildRequestString(fingerprint, duration, meta);

                // If the request contains invalid parameters, the server will return "400 Bad Request" and
                // we'll end up in the first catch block.
                string response = RequestService(request);

                return parser.ParseLookupResponse(response);
            }
            catch (WebException e)
            {
                // Handle bad requests gracefully.
                return CreateErrorResponse(e.Response as HttpWebResponse);
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        private LookupResponse CreateErrorResponse(HttpWebResponse response)
        {
            if (response == null)
            {
                return new LookupResponse(HttpStatusCode.BadRequest, "Unknown error.");
            }

            using (var reader = new StreamReader(response.GetResponseStream()))
            {
                var text = reader.ReadToEnd();

                if (parser.CanParse(text))
                {
                    return parser.ParseLookupResponse(text);
                }

                // TODO: parse error message (JSON).
                return new LookupResponse(response.StatusCode, text);
            }
        }

        private string BuildRequestString(string fingerprint, int duration, string[] meta)
        {
            StringBuilder request = new StringBuilder();

            request.Append("client=" + Configuration.ApiKey);

            if (meta != null)
            {
                request.Append("&meta=" + string.Join("+", meta));
            }

            request.Append("&format=" + parser.Format);
            request.Append("&duration=" + duration);
            request.Append("&fingerprint=" + fingerprint);

            return request.ToString();
        }

        private string RequestService(string request)
        {
            WebClient client = new WebClient();
            client.Headers.Add("User-Agent", "AcoustId.Net/" + ChromaContext.Version);
            client.Proxy = null;

            // For small data size, gzip will increase number of bytes to send.
            if (this.UseCompression && request.Length > 1800)
            {
                // The stream to hold the gzipped bytes.
                using (var stream = new MemoryStream())
                {
                    var encoding = Encoding.UTF8;

                    byte[] data = encoding.GetBytes(request);

                    // Create gzip stream
                    using (GZipStream gzip = new GZipStream(stream, CompressionMode.Compress))
                    {
                        gzip.Write(data, 0, data.Length);
                        gzip.Close();
                    }

                    double ratio = 1 / (double)data.Length;

                    data = stream.ToArray();

                    // ratio = (compressed size) / (uncompressed size)
                    ratio *= data.Length;

                    if (ratio > 0.95)
                    {
                        // Use standard get request
                        return client.DownloadString(URL + "?" + request);
                    }

                    client.Headers.Add("Content-Encoding", "gzip");
                    client.Headers.Add("Content-Type", "application/x-www-form-urlencoded");

                    data = client.UploadData(URL, data);

                    return encoding.GetString(data);
                }
            }
            else
            {
                return client.DownloadString(URL + "?" + request);
            }
        }
    }
}
