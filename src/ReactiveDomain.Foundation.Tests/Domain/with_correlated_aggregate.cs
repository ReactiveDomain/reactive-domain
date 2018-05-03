using System;
using ReactiveDomain.Messaging;
using Xunit;

// ReSharper disable once CheckNamespace
namespace ReactiveDomain.Foundation.Tests {
    // ReSharper disable once InconsistentNaming
    public class with_correlated_aggregate {
        readonly CorrelatedMessage _command = CorrelatedMessage.NewRoot();
        [Fact]
        public void can_raise_correlated_events() {
            var agg = new CorrelatedAggregate();
            agg.ApplyNewSource(_command);
            agg.RaiseCorrelatedEvent();
            agg.RaiseCorrelatedEventByIds();
            var events = agg.TakeEvents();
            Assert.Equal(2, events.Length);
            var @event = events[0] as CorrelatedAggregate.CorrelatedEvent;
            Assert.NotNull(@event);
            Assert.Equal(_command.CorrelationId, @event.CorrelationId);
            Assert.Equal(_command.MsgId, @event.SourceId);

            @event = events[1] as CorrelatedAggregate.CorrelatedEvent;
            Assert.NotNull(@event);
            Assert.Equal(_command.CorrelationId, @event.CorrelationId);
            Assert.Equal(_command.MsgId, @event.SourceId);
        }
        [Fact]
        public void cannot_raise_uncorrelated_events() {
            var agg = new CorrelatedAggregate();
            Assert.Throws<InvalidOperationException>(() => agg.RaiseUncorrelatedEvent());
            agg.ApplyNewSource(_command);
            Assert.Throws<InvalidOperationException>(() => agg.RaiseUncorrelatedEvent());
            Assert.Empty(agg.TakeEvents());
        }
        [Fact]
        public void cannot_raise_externally_correlated_events() {
            var otherRoot = CorrelatedMessage.NewRoot();
            var agg = new CorrelatedAggregate();
            Assert.Throws<InvalidOperationException>(() => agg.RaiseExternallyCorrelatedEvent(otherRoot));
            Assert.Throws<InvalidOperationException>(() => agg.RaiseExternallyCorrelatedEvent(otherRoot.CorrelationId, new SourceId(otherRoot)));
            agg.ApplyNewSource(_command);
            Assert.Throws<InvalidOperationException>(() => agg.RaiseExternallyCorrelatedEvent(otherRoot));
            Assert.Throws<InvalidOperationException>(() => agg.RaiseExternallyCorrelatedEvent(otherRoot.CorrelationId, new SourceId(otherRoot)));
            Assert.Empty(agg.TakeEvents());
        }
        [Fact]
        public void cannot_raise_events_without_source() {
            var otherRoot = CorrelatedMessage.NewRoot();
            var agg = new CorrelatedAggregate();
            Assert.Throws<NullReferenceException>(() => agg.RaiseCorrelatedEvent());
            Assert.Throws<InvalidOperationException>(() => agg.RaiseUncorrelatedEvent());
            Assert.Throws<InvalidOperationException>(() => agg.RaiseExternallyCorrelatedEvent(otherRoot));
            agg.ApplyNewSource(otherRoot);
            agg.RaiseCorrelatedEvent();
            var evt = agg.TakeEvents()[0] as CorrelatedAggregate.CorrelatedEvent;
            Assert.NotNull(evt);
            Assert.Throws<NullReferenceException>(() => agg.RaiseCorrelatedEvent());
            Assert.Empty(agg.TakeEvents());
        }
        [Fact]
        public void taking_events_removes_source() {
            var agg = new CorrelatedAggregate();
            agg.ApplyNewSource(_command);
            agg.RaiseCorrelatedEvent();
            var evt = agg.TakeEvents()[0] as CorrelatedAggregate.CorrelatedEvent;
            Assert.NotNull(evt);
            Assert.Throws<NullReferenceException>(() => agg.RaiseCorrelatedEvent());
            Assert.Empty(agg.TakeEvents());
        }
        [Fact]
        public void after_taking_events_new_source_may_be_applied() {
            var agg = new CorrelatedAggregate();
            agg.ApplyNewSource(_command);
            agg.RaiseCorrelatedEvent();
            var evt = agg.TakeEvents()[0] as CorrelatedAggregate.CorrelatedEvent;
            Assert.NotNull(evt);
            Assert.Equal(_command.CorrelationId, evt.CorrelationId);
            Assert.Equal(_command.MsgId, evt.SourceId);
            Assert.Throws<NullReferenceException>(() => agg.RaiseCorrelatedEvent());
            Assert.Empty(agg.TakeEvents());
            var otherRoot = CorrelatedMessage.NewRoot();
            agg.ApplyNewSource(otherRoot);
            agg.RaiseCorrelatedEvent();
            evt = agg.TakeEvents()[0] as CorrelatedAggregate.CorrelatedEvent;
            Assert.NotNull(evt);
            Assert.Equal(otherRoot.CorrelationId, evt.CorrelationId);
            Assert.Equal(otherRoot.MsgId, evt.SourceId);
        }
    }
}
