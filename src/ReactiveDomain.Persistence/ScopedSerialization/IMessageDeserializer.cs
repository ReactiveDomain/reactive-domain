using ReactiveDomain.Messaging;
using System;
using System.Collections.Generic;

namespace ReactiveDomain
{
	public interface IMessageDeserializer
	{
		Func<SerializedMessage, IEnumerable<Message>> CreateDeserializer<T>();
	}
}