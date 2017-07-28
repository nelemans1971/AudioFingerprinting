// -----------------------------------------------------------------------
// <copyright file="XmlResponseParser.cs" company="">
// Christian Woltering, https://github.com/wo80
// </copyright>
// -----------------------------------------------------------------------

namespace AcoustID.Web
{
    using System;
    using System.Globalization;
    using System.Net;
    using System.Xml.Linq;

    /// <summary>
    /// Parses lookup and submit responses from the webservice (XML format).
    /// </summary>
    public class XmlResponseParser : IResponseParser
    {
        private static NumberFormatInfo numberFormat = CultureInfo.InvariantCulture.NumberFormat;
        private static string format = "xml";

        /// <inheritdoc />
        public string Format
        {
            get { return format; }
        }
        
        /// <inheritdoc />
        public bool CanParse(string text)
        {
            return !string.IsNullOrEmpty(text) && text.StartsWith("<?xml");
        }

        /// <inheritdoc />
        public LookupResponse ParseLookupResponse(string text)
        {
            try
            {
                var root = XDocument.Parse(text).Element("response");

                var status = root.Element("status");

                if (status.Value == "ok")
                {
                    var response = new LookupResponse();

                    var list = root.Element("results").Descendants("result");

                    foreach (var item in list)
                    {
                        response.Results.Add(ParseLookupResult(item));
                    }

                    return response;
                }
                
                if (status.Value == "error")
                {
                    var error = root.Element("error");

                    return new LookupResponse(HttpStatusCode.BadRequest, error.Element("message").Value);
                }

                return null;
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        /// <summary>
        /// Parse the response of a submit request.
        /// </summary>
        /// <param name="response">The response string.</param>
        /// <returns>List of submit results.</returns>
        public SubmitResponse ParseSubmitResponse(string response)
        {
            // TODO: implement submit response parsing
            throw new NotImplementedException();
        }

        #region Lookup response

        private LookupResult ParseLookupResult(XElement el)
        {
            double score = 0.0;
            string id = string.Empty;

            XElement element = el.Element("score");

            if (element != null)
            {
                score = double.Parse(element.Value, numberFormat);
            }

            element = el.Element("id");

            if (element != null)
            {
                id = element.Value;
            }

            LookupResult result = new LookupResult(id, score);

            element = el.Element("recordings");

            if (element != null)
            {
                var recordings = element.Elements("recording");

                foreach (var recording in recordings)
                {
                    result.Recordings.Add(ParseRecording(recording));
                }
            }

            return result;
        }

        private Recording ParseRecording(XElement el)
        {
            int duration = 0;
            string id = string.Empty;
            string title = string.Empty;

            XElement element = el.Element("duration");

            if (element != null)
            {
                duration = int.Parse(element.Value);
            }

            element = el.Element("id");

            if (element != null)
            {
                id = element.Value;
            }

            element = el.Element("title");

            if (element != null)
            {
                title = element.Value;
            }

            var recording = new Recording(duration, id, title);

            element = el.Element("artists");

            if (element != null)
            {
                var list = element.Elements("artist");

                foreach (var item in list)
                {
                    recording.Artists.Add(ParseArtist(item));
                }
            }

            element = el.Element("releasegroups");

            if (element != null)
            {
                var list = element.Elements("releasegroup");

                foreach (var item in list)
                {
                    recording.ReleaseGroups.Add(ParseReleaseGroup(item));
                }
            }

            // TODO: parse more meta
            return recording;
        }

        private Artist ParseArtist(XElement el)
        {
            string id = string.Empty;
            string name = string.Empty;

            XElement element = el.Element("name");

            if (element != null)
            {
                name = element.Value;
            }

            element = el.Element("id");

            if (element != null)
            {
                id = element.Value;
            }

            return new Artist(id, name);
        }

        private ReleaseGroup ParseReleaseGroup(XElement el)
        {
            string id = string.Empty;
            string title = string.Empty;
            string type = string.Empty;

            XElement element = el.Element("id");

            if (element != null)
            {
                id = element.Value;
            }

            element = el.Element("title");

            if (element != null)
            {
                title = element.Value;
            }

            element = el.Element("type");

            if (element != null)
            {
                type = element.Value;
            }

            var releasegroup = new ReleaseGroup(id, title, type);

            element = el.Element("artists");

            if (element != null)
            {
                var list = element.Elements("artist");

                foreach (var item in list)
                {
                    releasegroup.Artists.Add(ParseArtist(item));
                }
            }

            return releasegroup;
        }

        #endregion
    }
}
