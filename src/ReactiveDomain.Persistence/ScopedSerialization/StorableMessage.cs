using System;

namespace ReactiveDomain
{
	public readonly struct StorableMessage
	{
		public readonly Guid Id;
		public readonly string Name;
		public readonly byte[] Data;
		public readonly byte[] Metadata;

		public StorableMessage(Guid id, string name, byte[] data, byte[] metadata)
		{
			Id = id;
			Name = name;
			Data = data;
			Metadata = metadata;
		}
	}
}