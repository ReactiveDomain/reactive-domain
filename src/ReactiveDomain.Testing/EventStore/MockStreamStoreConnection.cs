using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.ClientAPI.Exceptions;
using ReactiveDomain.Util;

// ReSharper disable MemberCanBePrivate.Global
namespace ReactiveDomain.Testing.EventStore {
    public class MockStreamStoreConnection : IStreamStoreConnection {
        public const string CategoryStreamNamePrefix = @"$ce";
        public const string EventTypeStreamNamePrefix = @"$et";
        public const string AllStreamName = @"$All";
        private readonly Dictionary<string, List<RecordedEvent>> _store;
        private List<RecordedEvent> All { get { lock (_store) { return _store[AllStreamName]; } } }
        private readonly QueuedHandler _inboundEventHandler;
        private readonly IBus _inboundEventBus;
        private readonly List<IDisposable> _subscriptions;
        private bool _connected;
        private bool _disposed;

        public MockStreamStoreConnection(string name) {
            _subscriptions = new List<IDisposable>();

            _store = new Dictionary<string, List<RecordedEvent>> { { AllStreamName, new List<RecordedEvent>() } };
            _inboundEventBus = new InMemoryBus(nameof(_inboundEventBus), false);
            _subscriptions.Add(_inboundEventBus.Subscribe(new AdHocHandler<EventWritten>(WriteToByCategoryProjection)));
            _subscriptions.Add(_inboundEventBus.Subscribe(new AdHocHandler<EventWritten>(WriteToByEventProjection)));

            _inboundEventHandler = new QueuedHandler(
                new AdHocHandler<Message>(_inboundEventBus.Publish),
                nameof(_inboundEventHandler),
                false);
            _inboundEventHandler.Start();
            ConnectionName = name;
        }


        public string ConnectionName { get; }

        public void Connect() {
            _connected = true;
        }

        public void Close() {
            _connected = false;
        }

        public event EventHandler<ClientConnectionEventArgs> Connected = (p1, p2) => { };

        public WriteResult AppendToStream(
            string stream,
            long expectedVersion,
            UserCredentials credentials = null,
            params EventData[] events) {

            if (!_connected) throw new Exception("Not Connected");
            if (_disposed) throw new ObjectDisposedException(nameof(MockStreamStoreConnection));
            if (string.IsNullOrWhiteSpace(stream))
                throw new ArgumentNullException(nameof(stream), $"{nameof(stream)} cannot be null or whitespace");

            List<RecordedEvent> eventStream;
            if (expectedVersion == ExpectedVersion.Any) {
                eventStream = GetOrCreateStream(stream);
            }
            else {
                bool streamExists;
                lock (_store) {
                    streamExists = _store.TryGetValue(stream, out eventStream);
                }

                if (streamExists && expectedVersion == ExpectedVersion.NoStream)
                    throw new WrongExpectedVersionException($"Stream {stream} exists, expected no stream");
                if (!streamExists && (expectedVersion == ExpectedVersion.StreamExists))
                    throw new WrongExpectedVersionException($"Stream {stream} does not exist, expected stream");

                if (!streamExists) {
                    eventStream = new List<RecordedEvent>();
                    lock (_store) {
                        _store.Add(stream, eventStream);
                    }
                }
                var startingPosition = eventStream.Count - 1;
                if (expectedVersion != ExpectedVersion.NoStream &&
                    expectedVersion != startingPosition)
                    throw new WrongExpectedVersionException(
                        $"Stream {stream} at position {eventStream.Count} expected {expectedVersion}.");
            }
            var epochStart = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            for (var i = 0; i < events.Length; i++) {
                var created = DateTime.UtcNow;
                var epochTime = (long)(created - epochStart).TotalSeconds;
                var recordedEvent = new RecordedEvent(
                    stream,
                    events[i].EventId,
                    eventStream.Count,
                    events[i].EventType,
                    events[i].Data,
                    events[i].Metadata,
                    events[i].IsJson,
                    created,
                    epochTime);
                eventStream.Add(recordedEvent);
                All.Add(recordedEvent);
                _inboundEventHandler.Handle(new EventWritten(stream, recordedEvent, false, recordedEvent.EventNumber));
            }
            return new WriteResult(eventStream.Count - 1);

        }

        /// <summary>
        /// Get or Create the By-Category projection Stream for Category
        /// </summary>
        /// <remarks>
        /// <see href="https://eventstore.org/docs/projections/system-projections/index.html"/>
        /// The category stream name is generated by spiting the event stream at the first dash. This is the default 
        /// configuration setting (prefix (a category) by splitting a stream id by a configurable separator.)
        /// </remarks>
        /// <param name="event">Original event whose name is in the aggregate-GUID format</param>
        /// <param name="streamName">the generated name for the stream</param>
        /// <returns>categoryStreamName: string</returns>
        private List<RecordedEvent> GetOrCreateCategoryStream(EventWritten @event, out string streamName) {
            var category = @event.StreamName.Split('-')[0];
            streamName = $"{CategoryStreamNamePrefix}-{category}";
            return GetOrCreateStream(streamName);
        }

        /// <summary>
        /// Get or Create the EventType Projection Stream for Event Type
        /// </summary>
        /// <remarks>
        /// <see href="https://eventstore.org/docs/projections/system-projections/index.html"/>
        /// The event stream name is generated from the event type. In the event store, this is not configurable.
        /// </remarks>
        /// <param name="event">Original event whose name is in the aggregate-GUID format</param>
        /// <param name="streamName">the generated name for the stream</param>
        /// <returns>eventStreamName: string</returns>
        private List<RecordedEvent> GetOrCreateEventTypeStream(EventWritten @event, out string streamName) {
            streamName = $"{EventTypeStreamNamePrefix}-{@event.Event.EventType}";
            return GetOrCreateStream(streamName);
        }
        private List<RecordedEvent> GetOrCreateStream(string streamName) {
            List<RecordedEvent> stream;
            lock (_store) {
                if (!_store.TryGetValue(streamName, out stream)) {
                    stream = new List<RecordedEvent>();
                    _store.Add(streamName, stream);
                }
            }
            return stream;
        }
        public StreamEventsSlice ReadStreamForward(
                                    string streamName,
                                    long start,
                                    long count,
                                    UserCredentials credentials = null) {
            if (start < 0) throw new ArgumentOutOfRangeException($"{nameof(start)} must be positve.");
            List<RecordedEvent> stream;
            lock (_store) {
                if (!_store.ContainsKey(streamName)) { return new StreamNotFoundSlice(streamName); }
                stream = _store[streamName].ToList();
            }
            return ReadFromStream(streamName, start, count, stream, ReadDirection.Forward);
        }
        public StreamEventsSlice ReadStreamBackward(
                                    string streamName,
                                    long start,
                                    long count,
                                    UserCredentials credentials = null) {
            if (start < 0) throw new ArgumentOutOfRangeException($"{nameof(start)} must be positve.");
            List<RecordedEvent> stream;
            lock (_store) {
                if (!_store.ContainsKey(streamName)) { return new StreamNotFoundSlice(streamName); }
                stream = _store[streamName].ToList();
            }
            return ReadFromStream(streamName, start, count, stream, ReadDirection.Backward);
        }
        private StreamEventsSlice ReadFromStream(
                                    string streamName,
                                    long start,
                                    long count,
                                    List<RecordedEvent> stream,
                                    ReadDirection direction) {
            var result = new List<RecordedEvent>();
            var next = (int)start;
            for (int i = 0; i < count; i++) {
                if (next < stream.Count && next >= 0) {
                    long current = next;
                    result.Add(stream[(int)current]);
                }
                next += (int)direction;
            }

            bool isEnd;

            if (direction == ReadDirection.Forward) {
                isEnd = next >= stream.Count;
                if (next > stream.Count) {
                    next = stream.Count;
                }
            }
            else  //Direction.Backward
            {
                isEnd = next < 0;
                if (next < 0) {
                    next = StreamPosition.End;
                }
                if (next > stream.Count + 1) {
                    next = stream.Count - 1;
                }
                else if (next > stream.Count) {
                    next = stream.Count;
                }
            }
            var slice = new StreamEventsSlice(
                                streamName,
                                start,
                                direction,
                                result.ToArray(),
                                next,
                                stream.Count - 1,
                                isEnd);

            return slice;
        }

        public sealed class Subscription :
            IHandle<EventWritten>,
            IDisposable {
            private readonly string _streamName;
            private long _position;
            private readonly Action<SubscriptionDropReason, Exception> _subscriptionDropped;
            private readonly Action<RecordedEvent> _eventAppeared;
            public IDisposable BusSubscription;

            public Subscription(
                string streamName,
                long startPosition,
                Action<SubscriptionDropReason, Exception> subscriptionDropped,
                Action<RecordedEvent> eventAppeared) {
                _streamName = streamName;
                _position = startPosition;
                _subscriptionDropped = subscriptionDropped;
                _eventAppeared = eventAppeared;
            }
            public void Handle(EventWritten evt) {
                if (string.CompareOrdinal(_streamName, evt.StreamName) == 0) {
                    if (evt.RecordedPosition > _position) {
                        _position = evt.RecordedPosition;
                        _eventAppeared(evt.Event);
                    }
                }
            }
            private bool _disposed;
            public void Dispose() {
                if (_disposed) return;
                _disposed = true;
                _subscriptionDropped?.Invoke(SubscriptionDropReason.UserInitiated, null);
                BusSubscription?.Dispose();
            }
        }

        public IDisposable SubscribeToStream(
            string stream,
            Action<RecordedEvent> eventAppeared,
            Action<SubscriptionDropReason, Exception> subscriptionDropped = null,
            UserCredentials userCredentials = null) {
            long currentPos = 0;
            lock (_store) {
                if (_store.ContainsKey(stream)) {
                    currentPos = _store[stream].Count - 1;
                }
            }

            return SubscribeToStreamFrom(
                stream,
                currentPos,
                CatchUpSubscriptionSettings.Default,
                eventAppeared,
                null, //live processing started
                subscriptionDropped,
                userCredentials);
        }
        public IDisposable SubscribeToStreamFrom(
            string stream,
            long? lastCheckpoint,
            CatchUpSubscriptionSettings settings,
            Action<RecordedEvent> eventAppeared,
            Action<Unit> liveProcessingStarted = null,
            Action<SubscriptionDropReason, Exception> subscriptionDropped = null,
            UserCredentials userCredentials = null) {

            var start = (lastCheckpoint ?? -1) + 1;
            RecordedEvent[] curEvents = { };
            lock (_store) {
                if (_store.ContainsKey(stream)) {
                    curEvents = _store[stream].Skip((int)start).ToArray();
                }
            }
            while (curEvents.Length > 0) {
                for (int i = 0; i < curEvents.Length; i++) {
                    eventAppeared(curEvents[i]);
                }

                start = start + curEvents.Length;
                lock (_store) {
                    curEvents = _store[stream].Skip((int)start).ToArray();
                }
            }
            //n.b. this leaves a possible gap in the events at switchover, #mock-life



            var subscription = new Subscription(stream, start - 1, subscriptionDropped, eventAppeared);
            subscription.BusSubscription = _inboundEventBus.Subscribe(subscription);
            _subscriptions.Add(subscription);
            liveProcessingStarted?.Invoke(Unit.Default);
            return subscription;

        }

        public IDisposable SubscribeToAll(
                                Action<RecordedEvent> eventAppeared,
                                Action<SubscriptionDropReason, Exception> subscriptionDropped = null,
                                UserCredentials userCredentials = null) {
            return SubscribeToAll(null, eventAppeared, null, subscriptionDropped, userCredentials);
        }

        public IDisposable SubscribeToAll(
            long? lastCheckpoint,
            Action<RecordedEvent> eventAppeared,
            Action<Unit> liveProcessingStarted = null,
            Action<SubscriptionDropReason, Exception> subscriptionDropped = null,
            UserCredentials userCredentials = null) {

            return SubscribeToStreamFrom(
                AllStreamName,
                lastCheckpoint,
                CatchUpSubscriptionSettings.Default,
                eventAppeared,
                liveProcessingStarted,
                subscriptionDropped,
                userCredentials);
        }

        public void DeleteStream(string stream, int expectedVersion, UserCredentials credentials = null) {
            if (stream.StartsWith(CategoryStreamNamePrefix, StringComparison.OrdinalIgnoreCase) ||
               stream.StartsWith(EventTypeStreamNamePrefix, StringComparison.OrdinalIgnoreCase)) {
                throw new ArgumentOutOfRangeException(nameof(stream), $"Deleting {stream} failed. Cannot delete standard projection streams");
            }
            if (stream.StartsWith("$")) {
                throw new AggregateException(new AccessDeniedException($"Write permission denied for {stream}."));
            }
            lock (_store) {
                if (_store.ContainsKey(stream)) {
                    _store.Remove(stream);
                }
                if (expectedVersion == ExpectedVersion.StreamExists) {
                    throw new ArgumentOutOfRangeException();
                }
            }
        }

        /// <summary>
        /// Write to the Category Stream
        /// </summary>
        /// <remarks>
        /// If the category stream doesn't exist, create it.
        /// The data in the event parameter includes the original stream, the recorded event, and messageId.
        /// <seealso cref="EventWritten"/>
        /// Generate the name from the original stream. <see cref="GetOrCreateCategoryStream"/>
        /// The caller sets up the Category Stream, append the events."/>
        /// </remarks>
        /// <param name="event">Event to be written to the category stream</param>
        private void WriteToByCategoryProjection(EventWritten @event) {
            if (!_connected || _disposed)
                throw new Exception();
            if (@event.ProjectedEvent) { return; }
            var stream = GetOrCreateCategoryStream(@event, out var streamName);
            var epochStart = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var created = DateTime.UtcNow;
            var epochTime = (long)(created - epochStart).TotalSeconds;

            var projectedEvent = new ProjectedEvent(
                    streamName,
                    stream.Count,
                    @event.Event.EventStreamId,
                    @event.Event.EventId, // reusing since the projection is linking to the original event
                    @event.Event.EventNumber,
                    @event.Event.EventType,
                    @event.Event.Data,
                    @event.Event.Metadata,
                    @event.Event.IsJson,
                    created,
                    epochTime);
            stream.Add(projectedEvent);
            All.Add(projectedEvent);
            _inboundEventHandler.Handle(new EventWritten(streamName, projectedEvent, true, projectedEvent.ProjectedEventNumber));
        }

        /// <summary>
        /// Write to the Event Stream
        /// </summary>
        /// <remarks>
        /// If the event stream doesn't exist, create it.
        /// The data in the event parameter includes the original stream, the recorded event, and messageId.
        /// <seealso cref="EventWritten"/>
        /// Generate the name from the event type passed in the original stream. <see cref="GetOrCreateEventTypeStream"/>
        /// The caller sets up the Event Stream, append the events."/>
        /// </remarks>
        /// <param name="event">Event to be written to the event stream</param>
        private void WriteToByEventProjection(EventWritten @event) {
            if (!_connected || _disposed)
                throw new Exception();
            if (@event.ProjectedEvent) { return; }
            var stream = GetOrCreateEventTypeStream(@event, out var streamName);
            var epochStart = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var created = DateTime.UtcNow;
            var epochTime = (long)(created - epochStart).TotalSeconds;

            var projectedEvent = new ProjectedEvent(
                streamName,
                stream.Count,
                @event.Event.EventStreamId,
                @event.Event.EventId, // reusing since the projection is linking to the original event
                @event.Event.EventNumber,
                @event.Event.EventType,
                @event.Event.Data,
                @event.Event.Metadata,
                @event.Event.IsJson,
                created,
                epochTime);
            stream.Add(projectedEvent);
            All.Add(projectedEvent);
            _inboundEventHandler.Handle(new EventWritten(streamName, projectedEvent, true, projectedEvent.ProjectedEventNumber));
        }


        public void Dispose() {
            if (_disposed) return;
            _disposed = true;
            Close();
            _subscriptions?.ForEach(s => s?.Dispose());
            _inboundEventHandler?.Stop();
        }

        public class EventWritten : Message {
            public readonly string StreamName;
            public readonly RecordedEvent Event;
            public readonly bool ProjectedEvent;
            public readonly long RecordedPosition;

            public EventWritten(
                string streamName,
                RecordedEvent @event,
                bool projectedEvent,
                long recordedPosition) {
                StreamName = streamName;
                Event = @event;
                ProjectedEvent = projectedEvent;
                RecordedPosition = recordedPosition;
            }
        }
    }
}
