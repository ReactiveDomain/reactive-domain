using ReactiveDomain.Messaging;
using ReactiveDomain.Util;

// ReSharper disable once CheckNamespace
namespace ReactiveDomain;

public abstract class AggregateRoot : EventDrivenStateMachine, ICorrelatedEventSource {
	protected AggregateRoot() { }

	protected AggregateRoot(ICorrelatedMessage? source = null) {
		if (source == null) { return; }
		Source = source;
	}

	internal void RegisterChild(ChildEntity childAggregate, out Action<object> raise, out EventRouter router) {
		Ensure.NotNull(childAggregate, nameof(childAggregate));
		raise = Raise;
		router = Router;
	}

	private Guid _correlationId;
	private Guid _causationId;
	public ICorrelatedMessage Source {
		get {
			return new CorrelatedRoot(_correlationId);
		}
		set {
			if (_correlationId != Guid.Empty && HasRecordedEvents) {
				throw new InvalidOperationException(
					"Cannot change source unless there are no recorded events, or current source is null");
			}
			_causationId = value.MsgId;
			_correlationId = value.CorrelationId != Guid.Empty
				? value.CorrelationId
				: value.MsgId; //root msg
		}
	}


	protected override void TakeEventStarted() {
		if (HasRecordedEvents && _correlationId == Guid.Empty) {
			throw new InvalidOperationException(
				"Cannot take events without valid source.");
		}
	}

	// One-shot: set by the repository's intermediate save immediately before the take it covers,
	// so the source survives exactly that save and the safe-by-default clearing returns after.
	private bool _continueSource;
	internal void ContinueSourceThroughNextTake() => _continueSource = true;

	protected override void TakeEventsCompleted() {
		if (!_continueSource) {
			_correlationId = Guid.Empty;
			_causationId = Guid.Empty;
		}
		_continueSource = false;
		base.TakeEventsCompleted();
	}
	protected override void OnEventRaised(object @event) {
		if (@event is ICorrelatedMessage correlatedEvent) {
			if (_correlationId == Guid.Empty) {
				throw new InvalidOperationException("Cannot raise events without valid source.");
			}
			if (correlatedEvent.CorrelationId != Guid.Empty || correlatedEvent.CausationId != Guid.Empty) {
				throw new InvalidOperationException("Cannot raise events with a different source.");
			}
			correlatedEvent.CorrelationId = _correlationId;
			correlatedEvent.CausationId = _causationId;
		} else {
			throw new InvalidOperationException("Cannot raise uncorrelated events from correlated Aggregate.");
		}
		base.OnEventRaised(@event);
	}
}
