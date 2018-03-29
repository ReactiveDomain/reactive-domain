using System;

namespace ReactiveDomain
{
    public struct WriteResult
    {
        /// <summary>The next expected version for the stream.</summary>
        public readonly long NextExpectedVersion;
        /// <summary>
        /// The <see cref="F:EventStore.ClientAPI.WriteResult.LogPosition" /> of the write.
        /// </summary>
        public readonly Position LogPosition;

        /// <summary>
        /// Constructs a new <see cref="T:EventStore.ClientAPI.WriteResult" />.
        /// </summary>
        /// <param name="nextExpectedVersion">The next expected version for the stream.</param>
        /// <param name="logPosition">The position of the write in the log</param>
        public WriteResult(long nextExpectedVersion, Position logPosition)
        {
            this.LogPosition = logPosition;
            this.NextExpectedVersion = nextExpectedVersion;
        }
    }
}
