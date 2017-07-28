// -----------------------------------------------------------------------
// <copyright file="IResponseParser.cs" company="">
// Christian Woltering, https://github.com/wo80
// </copyright>
// -----------------------------------------------------------------------

namespace AcoustID.Web
{
    using System.Collections.Generic;

    /// <summary>
    /// Parse the response of a AcoustId lookup or submit request.
    /// </summary>
    public interface IResponseParser
    {
        /// <summary>
        /// Gets the format of the response parser (must be "xml" or "json").
        /// </summary>
        string Format { get; }

        /// <summary>
        /// Indicates if the parser can read the given text format.
        /// </summary>
        /// <param name="text">The webservice response.</param>
        /// <returns>Returns true, if the parser can parse the given content.</returns>
        bool CanParse(string text);

        /// <summary>
        /// Parse the content of a lookup response.
        /// </summary>
        /// <param name="text">The webservice response.</param>
        /// <returns>A list of <see cref="LookupResult"/>.</returns>
        LookupResponse ParseLookupResponse(string text);

        /// <summary>
        /// Parse the content of a submit response.
        /// </summary>
        /// <param name="text">The webservice response.</param>
        /// <returns>A list of <see cref="SubmitResult"/>.</returns>
        SubmitResponse ParseSubmitResponse(string text);
    }
}
