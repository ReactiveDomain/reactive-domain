namespace ReactiveDomain
{
    /// <summary>
    /// The possible results of restoring a source of events from an event stream.
    /// </summary>
    public enum ReadResultState
    {
        /// <summary>
        /// The underlying stream was found.
        /// </summary>
        Found,
        /// <summary>
        /// The underlying stream was not found.
        /// </summary>
        NotFound,
        /// <summary>
        /// The underlying stream was deleted.
        /// </summary>
        Deleted
    }
}