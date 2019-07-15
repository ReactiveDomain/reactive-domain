using System;
using ReactiveDomain.Messaging;
using Xunit;

// ReSharper disable once CheckNamespace
namespace ReactiveDomain.Foundation.Tests
{
    // ReSharper disable once InconsistentNaming
    public class with_correlated_aggregate
    {
        readonly ICorrelatedMessage _command = MessageBuilder.New(() => new RootCommand());
        readonly ICorrelatedMessage _otherCommand = MessageBuilder.New(() => new OtherCommand());
        [Fact]
        public void can_raise_correlated_events_from_constructor_source()
        {
            var agg = new CorrelatedAggregate(Guid.NewGuid(), _command);
            agg.RaiseCorrelatedEvent();
            var events = agg.TakeEvents();
            Assert.Collection(events, new Action<object>[] {
                                                e =>{
                                                   if( e is CorrelatedAggregate.Created created){
                                                        Assert.Equal(_command.CorrelationId, created.CorrelationId);
                                                        Assert.Equal(_command.MsgId, created.CausationId);
                                                       }
                                                   else{
                                                        throw new Exception("wrong event");
                                                    };
                                                },
                                                 e =>{
                                                   if( e is CorrelatedAggregate.CorrelatedEvent @event){
                                                        Assert.Equal(_command.CorrelationId, @event.CorrelationId);                                                        
                                                       }
                                                   else{
                                                        throw new Exception("wrong event");
                                                    };
                                                }
            });
        }
        [Fact]
        public void can_raise_correlated_events_with_injected_source()
        {
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
        public void cannot_raise_uncorrelated_events()
        {
            var agg = new CorrelatedAggregate();
            Assert.Throws<InvalidOperationException>(() => agg.RaiseUncorrelatedEvent());
            ((ICorrelatedEventSource)agg).Source = _command;
            Assert.Throws<InvalidOperationException>(() => agg.RaiseUncorrelatedEvent());
            Assert.Empty(agg.TakeEvents());
        }
        [Fact]
        public void cannot_raise_externally_correlated_events()
        {
            var agg = new CorrelatedAggregate();
            Assert.Throws<InvalidOperationException>(() => agg.RaiseExternallyCorrelatedEvent(_otherCommand));
            ((ICorrelatedEventSource)agg).Source = _command;
            Assert.Throws<InvalidOperationException>(() => agg.RaiseExternallyCorrelatedEvent(_otherCommand));
            Assert.Empty(agg.TakeEvents());
        }
        [Fact]
        public void cannot_raise_events_without_source()
        {
            var agg = new CorrelatedAggregate();
            Assert.Throws<InvalidOperationException>(() => agg.RaiseCorrelatedEvent());
            Assert.Throws<InvalidOperationException>(() => agg.RaiseUncorrelatedEvent());
            Assert.Throws<InvalidOperationException>(() => agg.RaiseExternallyCorrelatedEvent(_otherCommand));
            ((ICorrelatedEventSource)agg).Source = _otherCommand;
            agg.RaiseCorrelatedEvent();
            var evt = agg.TakeEvents()[0] as CorrelatedAggregate.CorrelatedEvent;
            Assert.NotNull(evt);
            Assert.Throws<InvalidOperationException>(() => agg.RaiseCorrelatedEvent());
            Assert.Empty(agg.TakeEvents());
        }
        [Fact]
        public void taking_events_removes_source()
        {
            var agg = new CorrelatedAggregate();
            ((ICorrelatedEventSource)agg).Source = _command;
            agg.RaiseCorrelatedEvent();
            var evt = agg.TakeEvents()[0] as CorrelatedAggregate.CorrelatedEvent;
            Assert.NotNull(evt);
            Assert.Throws<InvalidOperationException>(() => agg.RaiseCorrelatedEvent());
            Assert.Empty(agg.TakeEvents());
        }
        [Fact]
        public void after_taking_events_new_source_may_be_applied()
        {
            var agg = new CorrelatedAggregate();
            ((ICorrelatedEventSource)agg).Source = _command;
            agg.RaiseCorrelatedEvent();
            var evt = agg.TakeEvents()[0] as CorrelatedAggregate.CorrelatedEvent;
            Assert.NotNull(evt);
            Assert.Equal(_command.CorrelationId, evt.CorrelationId);
            Assert.Equal(_command.MsgId, evt.CausationId);
            Assert.Throws<InvalidOperationException>(() => agg.RaiseCorrelatedEvent());
            Assert.Empty(agg.TakeEvents());
            ((ICorrelatedEventSource)agg).Source = _otherCommand;
            agg.RaiseCorrelatedEvent();
            evt = agg.TakeEvents()[0] as CorrelatedAggregate.CorrelatedEvent;
            Assert.NotNull(evt);
            Assert.Equal(_otherCommand.CorrelationId, evt.CorrelationId);
            Assert.Equal(_otherCommand.MsgId, evt.CausationId);
        }
        public class RootCommand : ICorrelatedMessage
        {
            public Guid MsgId { get; private set; }
            public Guid CorrelationId { get; set; }
            public Guid CausationId { get; set; }
            public RootCommand()
            {
                MsgId = Guid.NewGuid();
            }
        }
        public class OtherCommand : ICorrelatedMessage
        {
            public Guid MsgId { get; private set; }
            public Guid CorrelationId { get; set; }
            public Guid CausationId { get; set; }
            public OtherCommand()
            {
                MsgId = Guid.NewGuid();
            }
        }
    }
}
