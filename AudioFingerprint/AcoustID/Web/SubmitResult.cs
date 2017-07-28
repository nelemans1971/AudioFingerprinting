// -----------------------------------------------------------------------
// <copyright file="SubmitResult.cs" company="">
// Christian Woltering, https://github.com/wo80
// </copyright>
// -----------------------------------------------------------------------

namespace AcoustID.Web
{
    /// <summary>
    /// Result of a submit request.
    /// </summary>
    public class SubmitResult
    {
        /// <summary>
        /// Gets the id of the submit.
        /// </summary>
        public int Id { get; private set; }

        /// <summary>
        /// Gets the index of the submit (only for batch submits).
        /// </summary>
        public int Index { get; private set; }

        /// <summary>
        /// Gets the status of the submit (pending or imported).
        /// </summary>
        public string Status { get; private set; }

        /// <summary>
        /// Gets the assigned AcoustId of the submit (available only if status is "imported").
        /// </summary>
        public string Result { get; private set; }
    }
}
