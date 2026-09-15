// ReSharper disable once CheckNamespace
namespace ReactiveDomain;

/// <summary>
/// The base class each process manager or aggregate's root entity should derive from.
/// </summary>
public abstract class EventDrivenStateMachine : IEventSource {
	private readonly EventRecorder _recorder;
	protected readonly EventRouter Router;
	public bool HasRecordedEvents => _recorder.HasRecordedEvents;

	public Guid Id { get; protected set; }

	public long Version { get; private set; }

	long IEventSource.ExpectedVersion {
		get => Version;
		set => Version = value;
	}

	/// <summary>
	/// Initializes an event source's routing and recording behavior.
	/// </summary>
	protected EventDrivenStateMachine() {
		_recorder = new EventRecorder();
		Router = new EventRouter();
		Version = -1;
	}

	public void RestoreFromEvents(IEnumerable<object> events) {
		ArgumentNullException.ThrowIfNull(events);
		if (_recorder.HasRecordedEvents)
			throw new InvalidOperationException("Restoring from events is not possible when an instance has recorded events.");

		foreach (var @event in events) {
			if (Version < 0) // new aggregates have an expected version of -1 or -2
				Version = 0; // got first event (zero based)
			else
				Version++;
			Router.Route(@event);
		}
	}
	public void UpdateWithEvents(IEnumerable<object> events, long expectedVersion) {
		ArgumentNullException.ThrowIfNull(events);
		if (Version < 0)
			throw new InvalidOperationException("Updating with events is not possible when an instance has no historical events.");
		if (Version != expectedVersion) {
			throw new InvalidOperationException("Expected version mismatch when updating ");
		}

		foreach (var @event in events) {
			Version++;
			Router.Route(@event);
		}
	}

	/// <summary>
	/// Returns all events from the EventRecorder since state was loaded or the last time TakeEvents was called.
	/// Clears the EventRecorder.
	/// Increment the Version/ExpectedVersion by the event count.
	/// <para/>
	/// After this operation additional events can be applied via RestoreFromEvents.
	/// Also, Events can continue to be Raised either before or after this call.
	/// <para/>
	/// TakeEventStarted will be called before the Recorder is queried.
	/// TakeEventCompleted will be called after the Recorder is queried and cleared.
	/// </summary>
	/// <returns>Array of Object containing the Events Raised by the Aggregate since it was loaded or the last time TakeEvents was called</returns>
	public object[] TakeEvents() {
		object[] taken = [];
		TakeEvents(events => taken = events);
		return taken;
	}

	/// <inheritdoc cref="IEventSource.TakeEvents(Action{object[]})"/>
	/// <remarks>
	/// <see cref="TakeEventStarted"/> runs before <paramref name="persist"/>; the recorder is cleared,
	/// <see cref="Version"/> advanced and <see cref="TakeEventsCompleted"/> run only after it returns.
	/// </remarks>
	public void TakeEvents(Action<object[]> persist) {
		ArgumentNullException.ThrowIfNull(persist);
		TakeEventStarted();
		var records = _recorder.RecordedEvents;
		persist(records);
		_recorder.Reset();
		Version += records.Length;
		TakeEventsCompleted();
	}
	protected virtual void TakeEventStarted() { }
	protected virtual void TakeEventsCompleted() { }

	/// <summary>
	/// Registers a route for the specified <typeparamref name="TEvent">type of event</typeparamref> to the logic that needs to be applied to this instance to support future behaviors.
	/// </summary>
	/// <typeparam name="TEvent">The type of event.</typeparam>
	/// <param name="route">The logic to route the event to.</param>
	protected void Register<TEvent>(Action<TEvent> route) {
		Router.RegisterRoute(route);
	}

	/// <summary>
	/// Registers a route for the specified <paramref name="typeOfEvent">type of event</paramref> to the logic that needs to be applied to this instance to support future behaviors.
	/// </summary>
	/// <param name="typeOfEvent">The type of event.</param>
	/// <param name="route">The logic to route the event to.</param>
	protected void Register(Type typeOfEvent, Action<object> route) {
		Router.RegisterRoute(typeOfEvent, route);
	}

	/// <summary>
	/// Registers <typeparamref name="TEvent"/> as a type this instance persists and never folds.
	/// </summary>
	/// <remarks>
	/// Use this instead of omitting <see cref="Register{TEvent}"/>: an unregistered raise is persisted
	/// and then silently skipped on every replay. This makes that choice visible at the registration
	/// site, and keeps <see cref="ThrowOnUnrouted"/> from firing for it.
	/// </remarks>
	protected void RegisterPassThrough<TEvent>() => Register<TEvent>(_ => { });

	/// <summary>
	/// When true, <see cref="Raise"/> and replay throw if no handler is registered for the event's type.
	/// Defaults to false: pass-through provenance raises are a legitimate pattern, and those sites
	/// should <see cref="RegisterPassThrough{TEvent}"/> rather than leave the type unregistered.
	/// </summary>
	protected bool ThrowOnUnrouted {
		get => Router.ThrowOnUnrouted;
		set => Router.ThrowOnUnrouted = value;
	}

	/// <summary>The event types this instance has a handler for, including pass-throughs.</summary>
	public IReadOnlyCollection<Type> RegisteredEventTypes => Router.RegisteredTypes;

	protected virtual void OnEventRaised(object @event) { }
	/// <summary>
	/// Raises the specified <paramref name="event"/> - applies it to this instance and records it in its history.
	/// </summary>
	/// <param name="event">The event to apply and record.</param>
	protected void Raise(object @event) {
		OnEventRaised(@event);
		Router.Route(@event);
		_recorder.Record(@event);
	}
}
