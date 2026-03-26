using System;
using System.Collections.Generic;
using ReactiveDomain.Messaging;

namespace ReactiveDomain;

public interface IMessageDeserializer {
	Func<SerializedMessage, IEnumerable<Message>> CreateDeserializer<T>();
}
