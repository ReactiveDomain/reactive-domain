using EventStore.ClientAPI;
using System;

namespace Shovel
{
    public class EventShovel
    {
        private readonly EventShovelConfig _eventShovelConfig;

        public EventShovel(EventShovelConfig eventShovelConfig)
        {
            _eventShovelConfig = eventShovelConfig;
        }

        public void Run()
        {
            var streamPosition = Position.Start;
            var maxCount = int.Parse(Bootstrap.ReadSetting("readBatchSize"));
            while (true)
            {
                var slice = _eventShovelConfig.SourceConnection
                    .ReadAllEventsForwardAsync(streamPosition, maxCount, false, _eventShovelConfig.SourceCredentials)
                    .Result;
                Console.WriteLine($"Read {slice.Events.Length} events");

                streamPosition = slice.NextPosition;
                foreach (var e in slice.Events)
                {
                    Console.WriteLine($"Creating new EventData for the event {e.Event.EventId}");
                    if (ShouldSkipEvent(e))
                    {
                        Console.WriteLine(
                            $"Event {e.Event.EventId} of the type {e.Event.EventType} in the stream {e.OriginalStreamId} was filtered out. Skipping it");
                        continue;
                    }

                    ResolvedEvent transformedEvent = _eventShovelConfig.EventTransformer?.Transform(e) ?? e;

                    var newEventData = new EventData(transformedEvent.Event.EventId,
                                                     transformedEvent.Event.EventType,
                                                     transformedEvent.Event.IsJson,
                                                     transformedEvent.Event.Data,
                                                     transformedEvent.Event.Metadata);

                    _eventShovelConfig.TargetConnection.AppendToStreamAsync(transformedEvent.OriginalStreamId,
                        ExpectedVersion.Any, _eventShovelConfig.TargetCredentials, newEventData);

                    Console.WriteLine($"Append event {transformedEvent.Event.EventId} to the stream {transformedEvent.OriginalStreamId}");
                }
                if (slice.IsEndOfStream)
                    break;
            }
        }

        private bool ShouldSkipEvent(ResolvedEvent e)
        {
            if (e.Event.EventType.StartsWith("$"))
            {
                Console.WriteLine(
                    $"Event {e.Event.EventId} of the type {e.Event.EventType} is internal event. Skipping it");
                return true;
            }

            if (_eventShovelConfig.StreamFilter.Count == 0 && _eventShovelConfig.EventTypeFilter.Count == 0 &&
                _eventShovelConfig.StreamWildcardFilter.Count == 0 &&
                _eventShovelConfig.EventTypeWildcardFilter.Count == 0)
            {
                return false;
            }

            bool streamFiltered = false;
            if (_eventShovelConfig.StreamFilter.Count != 0)
            {
                streamFiltered = true;
                foreach (var filter in _eventShovelConfig.StreamFilter)
                {
                    if (e.OriginalStreamId == filter)
                    {
                        streamFiltered = false;
                        break;
                    }
                }
            }

            bool streamWildcardFiltered = streamFiltered;
            if (_eventShovelConfig.StreamWildcardFilter.Count != 0)
            {
                streamWildcardFiltered = true;
                foreach (var filter in _eventShovelConfig.StreamWildcardFilter)
                {
                    if (e.OriginalStreamId.StartsWith(filter))
                    {
                        streamWildcardFiltered = false;
                        break;
                    }

                }
            }

            bool eventFiltered = false;
            if (_eventShovelConfig.EventTypeFilter.Count != 0)
            {
                eventFiltered = true;
                foreach (var filter in _eventShovelConfig.EventTypeFilter)
                {
                    if (e.Event.EventType == filter)
                    {
                        eventFiltered = false;
                        break;
                    }
                }
            }

            bool eventWildcardFiltered = eventFiltered;
            if(_eventShovelConfig.EventTypeWildcardFilter.Count != 0)
            {
                eventWildcardFiltered = true;
                foreach (var filter in _eventShovelConfig.EventTypeWildcardFilter)
                {
                    if (e.Event.EventType.StartsWith(filter))
                    {
                        eventWildcardFiltered = false;
                        break;
                    }
                }
            }

            return (streamFiltered && streamWildcardFiltered) || (eventFiltered && eventWildcardFiltered);
        }
    }
}
