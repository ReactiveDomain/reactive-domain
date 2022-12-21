using System;

namespace ReactiveDomain
{
	public class Message : IMetadataSource
	{
		[NonSerialized] 
		private Metadata _metadata;

		Metadata IMetadataSource.ReadMetadata() => _metadata;

		Metadata IMetadataSource.Initialize()
		{
			if(_metadata != null) throw new InvalidOperationException();
			_metadata = new Metadata();
			return _metadata;
		}

		void IMetadataSource.Initialize(Metadata md)
		{
			if(_metadata != null) 
				throw new InvalidOperationException();
			_metadata = md;
		}
	}
}