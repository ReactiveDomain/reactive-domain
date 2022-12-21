using System;

namespace ReactiveDomain
{
	public readonly struct SerializedMessage : IEquatable<SerializedMessage>
	{
		public readonly Guid Id;
		public readonly string Name;
		public readonly string StreamName;
		public readonly long StreamRevision;
		public readonly byte[] Data;
		public readonly byte[] Metadata;

		public static readonly SerializedMessage None = new SerializedMessage();

		public SerializedMessage(Guid id, string name, string streamName, long streamRevision, byte[] data, byte[] metadata)
		{
			Id = id;
			Name = name;
			StreamName = streamName;
			StreamRevision = streamRevision;
			Data = data;
			Metadata = metadata;
		}

		public bool Equals(SerializedMessage other)
		{
			return string.Equals(Name, other.Name) && string.Equals(StreamName, other.StreamName) && StreamRevision == other.StreamRevision;
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			return obj is SerializedMessage && Equals((SerializedMessage)obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				var hashCode = Name.GetHashCode();
				hashCode = (hashCode * 397) ^ StreamName.GetHashCode();
				hashCode = (hashCode * 397) ^ StreamRevision.GetHashCode();
				return hashCode;
			}
		}

		public static bool operator ==(SerializedMessage left, SerializedMessage right)
		{
			return left.Equals(right);
		}

		public static bool operator !=(SerializedMessage left, SerializedMessage right)
		{
			return !left.Equals(right);
		}
	}
}