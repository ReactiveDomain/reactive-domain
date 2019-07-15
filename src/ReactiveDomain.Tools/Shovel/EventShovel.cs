using EventStore.ClientAPI;

namespace Shovel
{
    public class EventShovel
    {
        private readonly IEventStoreConnection _sourceConnection;
        private readonly IEventStoreConnection _targetConnection;

        public EventShovel(IEventStoreConnection sourceConnection, IEventStoreConnection targetConnection)
        {
            _sourceConnection = sourceConnection;
            _targetConnection = targetConnection;
        }

        public void Run()
        {
            var streamPosition = 0;
            while (true)
            {
                var slice = _sourceConnection.ReadStreamEventsForwardAsync("$", streamPosition, 1000, false).Result;
                streamPosition += slice.Events.Length;
                foreach (var e in slice.Events)
                {
                    var newEventData = new EventData(e.Event.EventId, e.Event.EventType, e.Event.IsJson, e.Event.Data, e.Event.Metadata);
                    _targetConnection.AppendToStreamAsync(e.OriginalStreamId, ExpectedVersion.Any, newEventData);
                }
                if (slice.IsEndOfStream)
                    break;
            }
        }
    }
}
