// -----------------------------------------------------------------------
// <copyright file="ReleaseGroup.cs" company="">
// Christian Woltering, https://github.com/wo80
// </copyright>
// -----------------------------------------------------------------------

namespace AcoustID.Web
{
    using System.Collections.Generic;

    /// <summary>
    /// Represents a release group.
    /// </summary>
    public class ReleaseGroup
    {
        public ReleaseGroup(string id, string title, string type)
        {
            this.Id = id;
            this.Title = title;
            this.Type = type;

            this.Artists = new List<Artist>();
        }

        /// <summary>
        /// Gets the MusicBrainz id of the release group.
        /// </summary>
        public string Id { get; private set; }

        /// <summary>
        /// Gets the title of the release group.
        /// </summary>
        public string Title { get; private set; }

        /// <summary>
        /// Gets the type of the release group.
        /// </summary>
        public string Type { get; private set; }

        /// <summary>
        /// Gets the artists associated with the release group.
        /// </summary>
        public List<Artist> Artists { get; private set; }

        public override string ToString()
        {
            return this.Title;
        }
    }
}
