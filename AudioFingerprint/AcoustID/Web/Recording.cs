// -----------------------------------------------------------------------
// <copyright file="Recording.cs" company="">
// Christian Woltering, https://github.com/wo80
// </copyright>
// -----------------------------------------------------------------------

namespace AcoustID.Web
{
    using System.Collections.Generic;

    /// <summary>
    /// Represents a recording.
    /// </summary>
    public class Recording
    {
        public Recording(int duration, string id, string title)
        {
            this.Duration = duration;
            this.Id = id;
            this.Title = title;

            this.Artists = new List<Artist>();
            this.ReleaseGroups = new List<ReleaseGroup>();
        }

        /// <summary>
        /// Gets the duration of the recording (seconds).
        /// </summary>
        public int Duration { get; private set; }

        /// <summary>
        /// Gets the MusicBrainz id of the recording.
        /// </summary>
        public string Id { get; private set; }

        /// <summary>
        /// Gets the title of the recording.
        /// </summary>
        public string Title { get; private set; }

        /// <summary>
        /// Gets the artists associated with the recording.
        /// </summary>
        public List<Artist> Artists { get; private set; }

        /// <summary>
        /// Gets the release groups associated with the recording.
        /// </summary>
        public List<ReleaseGroup> ReleaseGroups { get; private set; }

        public override string ToString()
        {
            return this.Title;
        }
    }
}
