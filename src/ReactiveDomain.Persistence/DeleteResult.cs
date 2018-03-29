namespace ReactiveDomain
{
    public struct DeleteResult
    {
        /// <summary>
        /// The <see cref="F:EventStore.ClientAPI.DeleteResult.LogPosition" /> of the write.
        /// </summary>
        public readonly Position LogPosition;

        /// <summary>
        /// Constructs a new <see cref="T:EventStore.ClientAPI.DeleteResult" />.
        /// </summary>
        /// <param name="logPosition">The position of the write in the log</param>
        public DeleteResult(Position logPosition) {
            LogPosition = logPosition;
        }
    }
}
