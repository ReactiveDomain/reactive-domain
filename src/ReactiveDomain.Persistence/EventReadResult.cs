using ReactiveDomain.Util;

namespace ReactiveDomain;

/// <summary>
/// An event read result is the result of a single event read operation to The Stream Store.
/// </summary>
public class EventReadResult {
	/// <summary>
	/// The <see cref="T:ReactiveDomain.EventReadStatus" /> representing the status of this read attempt.
	/// </summary>
	public readonly EventReadStatus Status;
	/// <summary>The name of the stream read.</summary>
	public readonly string? Stream;
	/// <summary>The event number of the requested event.</summary>
	public readonly long EventNumber;
	/// <summary>
	/// The event read represented as <see cref="T:EventStore.ClientAPI.ResolvedEvent" />.
	/// </summary>
	public readonly RecordedEvent? Event;

	public EventReadResult(RecordedEvent @event) {
		Ensure.NotNull(@event, nameof(@event));
		Status = EventReadStatus.Success;
		Stream = @event.EventStreamId;
		EventNumber = @event.EventNumber;
		Event = @event;
	}

	public EventReadResult(EventReadStatus status) {
		Status = status;
	}
}

public class EventNotFoundResult() : EventReadResult(EventReadStatus.NotFound);
public class EventNoStreamResult() : EventReadResult(EventReadStatus.NoStream);
public class EventStreamDeletedResult() : EventReadResult(EventReadStatus.StreamDeleted);


/// <summary>
/// Enumeration representing the status of a single event read operation.
/// </summary>
public enum EventReadStatus {
	Success,
	NotFound,
	NoStream,
	StreamDeleted,
}
