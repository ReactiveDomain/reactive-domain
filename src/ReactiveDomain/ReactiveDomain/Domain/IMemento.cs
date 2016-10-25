using System;

namespace ReactiveDomain.Domain
{
    public interface IMemento
	{
		Guid Id { get; set; }
		int Version { get; set; }
	}
}