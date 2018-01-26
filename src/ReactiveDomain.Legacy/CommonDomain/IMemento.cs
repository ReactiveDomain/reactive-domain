using System;

namespace ReactiveDomain.Legacy.CommonDomain
{
    public interface IMemento
	{
		Guid Id { get; set; }
		int Version { get; set; }
	}
}