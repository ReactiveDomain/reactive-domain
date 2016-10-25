using System;
using System.Collections;

namespace ReactiveDomain.Domain
{
    public interface ISaga
	{
		Guid Id { get; }
		int Version { get; }

		void Transition(object message);

		ICollection GetUncommittedEvents();
		void ClearUncommittedEvents();

		ICollection GetUndispatchedMessages();
		void ClearUndispatchedMessages();
	}
}