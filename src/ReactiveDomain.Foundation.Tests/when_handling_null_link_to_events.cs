using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using ReactiveDomain.EventStore;
using Xunit;
using ES = EventStore.ClientAPI;

namespace ReactiveDomain.Foundation.Tests
{
    // ReSharper disable once InconsistentNaming
    public class when_handling_null_link_to_events
    {
        [Fact]
        public void to_recorded_events_skips_null_events()
        {
            var valid = CreateResolvedEvent("stream-1", Guid.NewGuid(), 0, "TestEvent");
            var nullEvt = default(ES.ResolvedEvent); // Event is null

            var result = new[] { valid, nullEvt, valid }.ToRecordedEvents();

            Assert.Equal(2, result.Length);
        }

        [Fact]
        public void to_recorded_events_returns_empty_when_all_null()
        {
            var nullEvt = default(ES.ResolvedEvent);

            var result = new[] { nullEvt, nullEvt }.ToRecordedEvents();

            Assert.Empty(result);
        }

        [Fact]
        public void to_recorded_events_preserves_all_when_none_null()
        {
            var evt1 = CreateResolvedEvent("stream-1", Guid.NewGuid(), 0, "Event1");
            var evt2 = CreateResolvedEvent("stream-1", Guid.NewGuid(), 1, "Event2");
            var evt3 = CreateResolvedEvent("stream-1", Guid.NewGuid(), 2, "Event3");

            var result = new[] { evt1, evt2, evt3 }.ToRecordedEvents();

            Assert.Equal(3, result.Length);
        }

        [Fact]
        public void to_recorded_events_handles_empty_array()
        {
            var result = Array.Empty<ES.ResolvedEvent>().ToRecordedEvents();

            Assert.Empty(result);
        }

        [Fact]
        public void to_recorded_events_preserves_event_number_from_original()
        {
            var esEvent = CreateEsRecordedEvent("stream-1", Guid.NewGuid(), 0, "TestEvent");
            var originalEvent = CreateEsRecordedEvent("stream-1", Guid.NewGuid(), 42, "TestEvent");
            var resolved = CreateResolvedEvent(esEvent, originalEvent);

            var result = new[] { resolved }.ToRecordedEvents();

            Assert.Single(result);
            Assert.Equal(42, result[0].EventNumber);
        }

        #region ES Type Construction Helpers

        private static ES.ResolvedEvent CreateResolvedEvent(
            string streamId, Guid eventId, long eventNumber, string eventType)
        {
            var esEvent = CreateEsRecordedEvent(streamId, eventId, eventNumber, eventType);
            return CreateResolvedEvent(esEvent, esEvent);
        }

        private static ES.ResolvedEvent CreateResolvedEvent(
            ES.RecordedEvent evt, ES.RecordedEvent originalEvent)
        {
            var resolved = default(ES.ResolvedEvent);
            object boxed = resolved;
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            var eventField = typeof(ES.ResolvedEvent).GetField("Event", flags);
            var linkField = typeof(ES.ResolvedEvent).GetField("Link", flags);
            eventField?.SetValue(boxed, evt);
            linkField?.SetValue(boxed, originalEvent);

            return (ES.ResolvedEvent)boxed;
        }

        private static ES.RecordedEvent CreateEsRecordedEvent(
            string streamId, Guid eventId, long eventNumber, string eventType)
        {
            var evt = (ES.RecordedEvent)RuntimeHelpers.GetUninitializedObject(typeof(ES.RecordedEvent));
            var type = typeof(ES.RecordedEvent);
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            type.GetField("EventStreamId", flags)?.SetValue(evt, streamId);
            type.GetField("EventId", flags)?.SetValue(evt, eventId);
            type.GetField("EventNumber", flags)?.SetValue(evt, eventNumber);
            type.GetField("EventType", flags)?.SetValue(evt, eventType);
            type.GetField("Data", flags)?.SetValue(evt, new byte[] { 0x7B, 0x7D }); // {}
            type.GetField("Metadata", flags)?.SetValue(evt, new byte[] { 0x7B, 0x7D }); // {}
            type.GetField("IsJson", flags)?.SetValue(evt, true);
            type.GetField("Created", flags)?.SetValue(evt, DateTime.UtcNow);
            type.GetField("CreatedEpoch", flags)?.SetValue(evt, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

            return evt;
        }

        #endregion
    }
}
