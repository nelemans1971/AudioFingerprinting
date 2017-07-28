// -----------------------------------------------------------------------
// <copyright file="Artist.cs" company="">
// Christian Woltering, https://github.com/wo80
// </copyright>
// -----------------------------------------------------------------------

namespace AcoustID.Web
{
    /// <summary>
    /// Represents an artist.
    /// </summary>
    public class Artist
    {
        public Artist(string id, string name)
        {
            this.Id = id;
            this.Name = name;
        }

        /// <summary>
        /// Gets the MusicBrainz id of the artist.
        /// </summary>
        public string Id { get; private set; }

        /// <summary>
        /// Gets the name of the artist.
        /// </summary>
        public string Name { get; private set; }

        public override string ToString()
        {
            return this.Name;
        }
    }
}
