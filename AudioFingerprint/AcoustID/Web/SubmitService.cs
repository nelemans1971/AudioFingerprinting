// -----------------------------------------------------------------------
// <copyright file="SubmitService.cs" company="">
// Christian Woltering, https://github.com/wo80
// </copyright>
// -----------------------------------------------------------------------

namespace AcoustID.Web
{
    using System;

    /// <summary>
    /// Calls the AcoustId webservice to submit a new fingerprint.
    /// </summary>
    public class SubmitService
    {
        private const string URL = "http://api.acoustid.org/v2/submit";

        private IResponseParser parser;

        public SubmitService()
            : this(new XmlResponseParser())
        {
        }

        public SubmitService(IResponseParser parser)
        {
            this.parser = parser;

            UseCompression = true;
        }

        /// <summary>
        /// Gets or sets a value indicating whether to compress the data before submit.
        /// </summary>
        public bool UseCompression { get; set; }

        // TODO: implement submit
    }
}
