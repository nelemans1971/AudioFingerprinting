// -----------------------------------------------------------------------
// <copyright file="LookupResult.cs" company="">
// Christian Woltering, https://github.com/wo80
// </copyright>
// -----------------------------------------------------------------------

namespace AcoustID.Web
{
    using System.Collections.Generic;

    /// <summary>
    /// Result of a lookup request.
    /// </summary>
    public class LookupResult
    {
        public LookupResult(string id, double score)
        {
            this.Id = id;
            this.Score = score;

            this.Recordings = new List<Recording>();
        }

        /// <summary>
        /// Gets the AcoustID of the lookup result.
        /// </summary>
        public string Id { get; private set; }

        /// <summary>
        /// Gets the score of the lookup result (between 0 and 1).
        /// </summary>
        public double Score { get; private set; }

        /// <summary>
        /// Gets the recordings of the lookup result.
        /// </summary>
        public List<Recording> Recordings { get; private set; }
    }
}
