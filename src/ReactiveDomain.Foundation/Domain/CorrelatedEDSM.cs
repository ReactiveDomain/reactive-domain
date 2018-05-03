using System;
using System.CodeDom;
using ReactiveDomain.Messaging;

// ReSharper disable once CheckNamespace
namespace ReactiveDomain
{
    public class CorrelatedEDSM: EventDrivenStateMachine
    {
        public CorrelatedEDSM() {}
        public CorrelatedEDSM(CorrelatedMessage source) {
            Source = source;
        }
        public CorrelatedMessage Source {
            get;
            protected set;
        }

        public void ApplyNewSource(CorrelatedMessage source) {
            if (Source != null || HasRecordedEvents) {
                throw new InvalidOperationException(
                    "Cannot change source unless there are no recorded events, and current source is null");
            }
            Source = source;
        }
        protected override void TakeEventStarted() {
            if (HasRecordedEvents && Source == null) {
                throw new InvalidOperationException(
                    "Cannot take events without valid source.");
            }
        }
        protected override void TakeEventsCompleted(){
            Source = null;
            base.TakeEventsCompleted();
        }
        protected override void OnEventRaised(object @event) {
            
            if (Source == null) {
                throw new InvalidOperationException(
                    "Cannot raise events without valid source.");
            }

            if (!(@event is CorrelatedMessage correlated) ||
                !correlated.CorrelationId.Equals(Source.CorrelationId) ||
                correlated.SourceId != Source.MsgId) {
                throw new InvalidOperationException(
                    $"Missing or mismatched source for event {@event.GetType().Name}.");
            }
            base.OnEventRaised(@event);
        }
    }
}
