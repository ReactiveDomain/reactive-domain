using EventStore.ClientAPI;

namespace Shovel
{
    using System;
    using EventStore.ClientAPI.SystemData;

    public class EventShovel
    {
        private readonly IEventStoreConnection _sourceConnection;
        private readonly IEventStoreConnection _targetConnection;
        private readonly UserCredentials _sourceCredentials;
        private readonly UserCredentials _targetCredentials;

        public EventShovel(IEventStoreConnection sourceConnection, IEventStoreConnection targetConnection,
            UserCredentials sourceCredentials, UserCredentials targetCredentials)
        {
            _sourceConnection = sourceConnection;
            _targetConnection = targetConnection;
            _sourceCredentials = sourceCredentials;
            _targetCredentials = targetCredentials;
        }

        public void Run()
        {
            var streamPosition = Position.Start;
            //var processedEvents = 0;
            var maxCount = 1000;
            while (true)
            {
                var slice = _sourceConnection.ReadAllEventsForwardAsync(streamPosition, maxCount, false, _sourceCredentials).Result;
                Console.WriteLine($"Read {slice.Events.Length} events");

                //processedEvents += slice.Events.Length;
                streamPosition = slice.NextPosition;
                foreach (var e in slice.Events)
                {
                    Console.WriteLine($"Creating new EventData for the event {e.Event.EventId}");
                    if (e.Event.EventType.StartsWith("$"))
                    {
                        Console.WriteLine($"Event {e.Event.EventId} of the type {e.Event.EventType} is internal event. Skipping it");
                        continue;
                    }

                    var newEventData = new EventData(e.Event.EventId, e.Event.EventType, e.Event.IsJson, e.Event.Data, e.Event.Metadata);
                    _targetConnection.AppendToStreamAsync(e.OriginalStreamId, ExpectedVersion.Any, _targetCredentials, newEventData);
                    Console.WriteLine($"Append event {e.Event.EventId} to the stream {e.OriginalStreamId}");
                }
                if (slice.IsEndOfStream)
                    break;
            }
        }
    }
}
