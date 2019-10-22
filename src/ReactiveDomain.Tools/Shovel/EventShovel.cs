using EventStore.ClientAPI;
using System;

namespace Shovel
{
    using System.Collections.Generic;

    public class EventShovel
    {
        private readonly EventShovelConfig _eventShovelConfig;

        public EventShovel(EventShovelConfig eventShovelConfig)
        {
            _eventShovelConfig = eventShovelConfig;
        }

        public void Run()
        {
            long startPosition = long.Parse(Bootstrap.ReadSetting("startPosition"));
            Position streamPosition = new Position(startPosition, 0);
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

                    if (_eventShovelConfig.EventTransformer != null)
                    {
                        ICollection<ResolvedEvent> transformedEvents = _eventShovelConfig.EventTransformer.Transform(e);
                        foreach (var evt in transformedEvents)
                        {
                            var newEventData = new EventData(evt.Event.EventId,
                                evt.Event.EventType,
                                evt.Event.IsJson,
                                evt.Event.Data,
                                evt.Event.Metadata);

                            _eventShovelConfig.TargetConnection.AppendToStreamAsync(evt.OriginalStreamId,
                                ExpectedVersion.Any, _eventShovelConfig.TargetCredentials, newEventData);
                            Console.WriteLine($"Append event {evt.Event.EventId} to the stream {evt.OriginalStreamId}");
                        }
                    }
                    else
                    {
                        var newEventData = new EventData(e.Event.EventId,
                            e.Event.EventType,
                            e.Event.IsJson,
                            e.Event.Data,
                            e.Event.Metadata);

                        _eventShovelConfig.TargetConnection.AppendToStreamAsync(e.OriginalStreamId,
                            ExpectedVersion.Any, _eventShovelConfig.TargetCredentials, newEventData);
                        Console.WriteLine($"Append event {e.Event.EventId} to the stream {e.OriginalStreamId}");
                    }
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

            bool skipForStreamFilter = false;
            if (_eventShovelConfig.StreamFilter.Count != 0)
            {
                skipForStreamFilter = true;
                foreach (var filter in _eventShovelConfig.StreamFilter)
                {
                    if (e.OriginalStreamId == filter)
                    {
                        skipForStreamFilter = false;
                        break;
                    }
                }
            }

            bool skipForStreamWildcardFilter = skipForStreamFilter;
            if (_eventShovelConfig.StreamWildcardFilter.Count != 0)
            {
                skipForStreamWildcardFilter = true;
                foreach (var filter in _eventShovelConfig.StreamWildcardFilter)
                {
                    if (e.OriginalStreamId.StartsWith(filter))
                    {
                        skipForStreamWildcardFilter = false;
                        break;
                    }

                }
            }

            bool skipForEventFilter = false;
            if (_eventShovelConfig.EventTypeFilter.Count != 0)
            {
                skipForEventFilter = true;
                foreach (var filter in _eventShovelConfig.EventTypeFilter)
                {
                    if (e.Event.EventType == filter)
                    {
                        skipForEventFilter = false;
                        break;
                    }
                }
            }

            bool skipForEventWildcardFilter = skipForEventFilter;
            if(_eventShovelConfig.EventTypeWildcardFilter.Count != 0)
            {
                skipForEventWildcardFilter = true;
                foreach (var filter in _eventShovelConfig.EventTypeWildcardFilter)
                {
                    if (e.Event.EventType.StartsWith(filter))
                    {
                        skipForEventWildcardFilter = false;
                        break;
                    }
                }
            }

            return (skipForStreamFilter && skipForStreamWildcardFilter) || (skipForEventFilter && skipForEventWildcardFilter);
        }
    }
}
