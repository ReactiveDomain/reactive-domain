using System;

namespace ReactiveDomain
{
	public class Serialization
	{
		public Serialization(SerializationPair<IMessageSerializer, IMessageDeserializer> messages, SerializationPair<ISnapshotSerializer, ISnapshotDeserializer> snapshots, Func<SerializedMessage, IMetadata> metadata)
		{
			Messages = messages;
			Snapshots = snapshots;
			Metadata = metadata;
		}

		public SerializationPair<IMessageSerializer, IMessageDeserializer> Messages { get; }
		public SerializationPair<ISnapshotSerializer, ISnapshotDeserializer> Snapshots { get; }

		public Func<SerializedMessage, IMetadata> Metadata { get; }
	}
}