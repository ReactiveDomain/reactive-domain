using System;

namespace ReactiveDomain
{
	public abstract class Snapshot : IMetadataSource
	{
		[NonSerialized]
		private Metadata _metadata;

		Metadata IMetadataSource.ReadMetadata()
		{
			return _metadata;
		}

		Metadata IMetadataSource.Initialize()
		{
			if(_metadata != null) throw new InvalidOperationException();
			return _metadata = new Metadata();
		}

		void IMetadataSource.Initialize(Metadata md)
		{
			_metadata = md;
		}
	}
}