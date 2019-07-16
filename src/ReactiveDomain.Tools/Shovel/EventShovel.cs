﻿using EventStore.ClientAPI;

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
        private readonly IEventTransformer _eventTransformer;

        public EventShovel(IEventStoreConnection sourceConnection, IEventStoreConnection targetConnection,
            UserCredentials sourceCredentials, UserCredentials targetCredentials, IEventTransformer eventTransformer)
        {
            _sourceConnection = sourceConnection;
            _targetConnection = targetConnection;
            _sourceCredentials = sourceCredentials;
            _targetCredentials = targetCredentials;
            _eventTransformer = eventTransformer;
        }

        public void Run()
        {
            var streamPosition = Position.Start;
            var maxCount = int.Parse(Bootstrap.ReadSetting("readBatchSize"));
            while (true)
            {
                var slice = _sourceConnection.ReadAllEventsForwardAsync(streamPosition, maxCount, false, _sourceCredentials).Result;
                Console.WriteLine($"Read {slice.Events.Length} events");

                streamPosition = slice.NextPosition;
                foreach (var e in slice.Events)
                {
                    Console.WriteLine($"Creating new EventData for the event {e.Event.EventId}");
                    if (e.Event.EventType.StartsWith("$"))
                    {
                        Console.WriteLine($"Event {e.Event.EventId} of the type {e.Event.EventType} is internal event. Skipping it");
                        continue;
                    }

                    ResolvedEvent transformedEvent = _eventTransformer != null ? _eventTransformer.Trasnform(e) : e;

                    var newEventData = new EventData(transformedEvent.Event.EventId,
                                                     transformedEvent.Event.EventType,
                                                     transformedEvent.Event.IsJson,
                                                     transformedEvent.Event.Data,
                                                     transformedEvent.Event.Metadata);

                    _targetConnection.AppendToStreamAsync(transformedEvent.OriginalStreamId, ExpectedVersion.Any, _targetCredentials, newEventData);
                    Console.WriteLine($"Append event {transformedEvent.Event.EventId} to the stream {transformedEvent.OriginalStreamId}");
                }
                if (slice.IsEndOfStream)
                    break;
            }
        }
    }
}