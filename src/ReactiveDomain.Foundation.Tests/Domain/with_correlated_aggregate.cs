using ReactiveDomain.Messaging;
using Xunit;

namespace ReactiveDomain.Foundation.Tests.Domain;

// ReSharper disable once InconsistentNaming
public class with_correlated_aggregate {
	private readonly RootCommand _command = MessageBuilder.New(() => new RootCommand());
	private readonly OtherCommand _otherCommand = MessageBuilder.New(() => new OtherCommand());

	[Fact]
	public void can_raise_correlated_events_from_constructor_source() {
		var agg = new CorrelatedAggregate(Guid.NewGuid(), _command);
		agg.RaiseCorrelatedEvent();
		var events = agg.TakeEvents();
		Assert.Collection(events,
			e => {
				if (e is CorrelatedAggregate.Created created) {
					Assert.Equal(_command.CorrelationId, created.CorrelationId);
					Assert.Equal(_command.MsgId, created.CausationId);
				} else {
					throw new Exception("wrong event");
				}
			},
			e => {
				if (e is CorrelatedAggregate.CorrelatedEvent @event) {
					Assert.Equal(_command.CorrelationId, @event.CorrelationId);
				} else {
					throw new Exception("wrong event");
				}
			});
	}

	[Fact]
	public void can_raise_correlated_events_with_injected_source() {
		var agg = new CorrelatedAggregate();
		((ICorrelatedEventSource)agg).Source = _command;
		agg.RaiseCorrelatedEvent();
		var events = agg.TakeEvents();
		Assert.Single(events);
		var @event = events[0] as CorrelatedAggregate.CorrelatedEvent;
		Assert.NotNull(@event);
		Assert.Equal(_command.CorrelationId, @event.CorrelationId);
		Assert.Equal(_command.MsgId, @event.CausationId);
	}

	[Fact]
	public void cannot_raise_uncorrelated_events() {
		var agg = new CorrelatedAggregate();
		Assert.Throws<InvalidOperationException>(agg.RaiseUncorrelatedEvent);
		((ICorrelatedEventSource)agg).Source = _command;
		Assert.Throws<InvalidOperationException>(agg.RaiseUncorrelatedEvent);
		Assert.Empty(agg.TakeEvents());
	}

	[Fact]
	public void cannot_raise_externally_correlated_events() {
		var agg = new CorrelatedAggregate();
		Assert.Throws<InvalidOperationException>(() => agg.RaiseExternallyCorrelatedEvent(_otherCommand));
		((ICorrelatedEventSource)agg).Source = _command;
		Assert.Throws<InvalidOperationException>(() => agg.RaiseExternallyCorrelatedEvent(_otherCommand));
		Assert.Empty(agg.TakeEvents());
	}

	[Fact]
	public void cannot_raise_events_without_source() {
		var agg = new CorrelatedAggregate();
		Assert.Throws<InvalidOperationException>(agg.RaiseCorrelatedEvent);
		Assert.Throws<InvalidOperationException>(agg.RaiseUncorrelatedEvent);
		Assert.Throws<InvalidOperationException>(() => agg.RaiseExternallyCorrelatedEvent(_otherCommand));
		((ICorrelatedEventSource)agg).Source = _otherCommand;
		agg.RaiseCorrelatedEvent();
		var evt = agg.TakeEvents()[0] as CorrelatedAggregate.CorrelatedEvent;
		Assert.NotNull(evt);
		Assert.Throws<InvalidOperationException>(agg.RaiseCorrelatedEvent);
		Assert.Empty(agg.TakeEvents());
	}

	[Fact]
	public void taking_events_removes_source() {
		var agg = new CorrelatedAggregate();
		((ICorrelatedEventSource)agg).Source = _command;
		agg.RaiseCorrelatedEvent();
		var evt = agg.TakeEvents()[0] as CorrelatedAggregate.CorrelatedEvent;
		Assert.NotNull(evt);
		Assert.Throws<InvalidOperationException>(agg.RaiseCorrelatedEvent);
		Assert.Empty(agg.TakeEvents());
	}

	[Fact]
	public void after_taking_events_new_source_may_be_applied() {
		var agg = new CorrelatedAggregate();
		((ICorrelatedEventSource)agg).Source = _command;
		agg.RaiseCorrelatedEvent();
		var evt = agg.TakeEvents()[0] as CorrelatedAggregate.CorrelatedEvent;
		Assert.NotNull(evt);
		Assert.Equal(_command.CorrelationId, evt.CorrelationId);
		Assert.Equal(_command.MsgId, evt.CausationId);
		Assert.Throws<InvalidOperationException>(agg.RaiseCorrelatedEvent);
		Assert.Empty(agg.TakeEvents());
		((ICorrelatedEventSource)agg).Source = _otherCommand;
		agg.RaiseCorrelatedEvent();
		evt = agg.TakeEvents()[0] as CorrelatedAggregate.CorrelatedEvent;
		Assert.NotNull(evt);
		Assert.Equal(_otherCommand.CorrelationId, evt.CorrelationId);
		Assert.Equal(_otherCommand.MsgId, evt.CausationId);
	}

	[Fact]
	public void taking_events_with_persist_commits_after_persist_returns() {
		var agg = new CorrelatedAggregate(Guid.NewGuid(), _command);
		agg.RaiseCorrelatedEvent();
		IEventSource source = agg;

		object[]? persisted = null;
		source.TakeEvents(events => {
			persisted = events;
			Assert.True(source.HasRecordedEvents);
			Assert.Equal(-1, source.ExpectedVersion);
		});

		Assert.NotNull(persisted);
		Assert.Equal(2, persisted.Length);
		Assert.False(source.HasRecordedEvents);
		Assert.Equal(1, source.ExpectedVersion);
		Assert.Throws<InvalidOperationException>(agg.RaiseCorrelatedEvent);
	}

	[Fact]
	public void failed_persist_leaves_events_version_and_source_intact() {
		var agg = new CorrelatedAggregate(Guid.NewGuid(), _command);
		agg.RaiseCorrelatedEvent();
		IEventSource source = agg;

		Assert.Throws<IOException>(() => source.TakeEvents(_ => throw new IOException()));

		Assert.True(source.HasRecordedEvents);
		Assert.Equal(-1, source.ExpectedVersion);

		agg.RaiseCorrelatedEvent();
		var events = source.TakeEvents();
		Assert.Equal(3, events.Length);
		Assert.Equal(2, source.ExpectedVersion);
		foreach (var evt in events.Cast<ICorrelatedMessage>()) {
			Assert.Equal(_command.CorrelationId, evt.CorrelationId);
			Assert.Equal(_command.MsgId, evt.CausationId);
		}
	}

	public record RootCommand : ICorrelatedMessage {
		public Guid MsgId { get; private set; } = Guid.NewGuid();
		public Guid CorrelationId { get; set; }
		public Guid CausationId { get; set; }
	}

	public record OtherCommand : ICorrelatedMessage {
		public Guid MsgId { get; private set; } = Guid.NewGuid();
		public Guid CorrelationId { get; set; }
		public Guid CausationId { get; set; }
	}
}
