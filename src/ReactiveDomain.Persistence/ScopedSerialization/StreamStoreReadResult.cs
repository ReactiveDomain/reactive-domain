using System.Collections.Generic;

namespace ReactiveDomain
{
	public class StreamStoreReadResult
	{
		public StreamStoreReadResult(IReadOnlyList<SerializedMessage> messages, bool isEndOfStream)
		{
			Messages = messages;
			IsEndOfStream = isEndOfStream;
		}

		public IReadOnlyList<SerializedMessage> Messages { get; }
		public bool IsEndOfStream { get; }
	}
}