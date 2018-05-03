using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ReactiveDomain.Util;

// ReSharper disable MemberCanBePrivate.Global
namespace ReactiveDomain.Testing.EventStore {
    public class MockStreamStoreConnection : IStreamStoreConnection
    {
        private readonly object _accessLock = new object();
        public const string CategoryStreamNamePrefix = @"$ce";
        public const string EventTypeStreamNamePrefix = @"$et";
        public const string AllStreamName = @"All";
        private readonly Dictionary<string, List<RecordedEvent>> _store;
        // ReSharper disable once CollectionNeverQueried.Local
        private readonly List<RecordedEvent> _all;
        private List<RecordedEvent> _category;
#pragma warning disable 169
        private List<RecordedEvent> _eventType;
#pragma warning restore 169
        private readonly QueuedHandler _inboundEventHandler;
        private readonly IBus _inboundEventBus;
        private bool _connected;
        private bool _disposed;

        public MockStreamStoreConnection(string name)
        {
            _all = new List<RecordedEvent>();
            _store = new Dictionary<string, List<RecordedEvent>>();
            _inboundEventBus = new InMemoryBus(nameof(_inboundEventBus), false);
            _inboundEventBus.Subscribe(new AdHocHandler<EventWritten>(WriteToCategoryStream));
            _inboundEventBus.Subscribe(new AdHocHandler<EventWritten>(WriteEventStream));
            _inboundEventBus.Subscribe(new AdHocHandler<ProjectedEventWritten>(PublishToSubscriptions));

            _inboundEventHandler = new QueuedHandler(
                new AdHocHandler<Message>(_inboundEventBus.Publish),
                nameof(_inboundEventHandler),
                false);
            _inboundEventHandler.Start();
            ConnectionName = name;
        }


        public string ConnectionName { get; }

        public void Connect()
        {
            _connected = true;
        }

        public void Close()
        {
            _connected = false;
        }

        public event EventHandler<ClientConnectionEventArgs> Connected = (p1, p2) => { };

        public WriteResult AppendToStream(
            string stream,
            long expectedVersion,
            UserCredentials credentials = null,
            params EventData[] events)
        {
            lock (_accessLock) {
                if (!_connected) throw new Exception("Not Connected");
                if( _disposed) throw new ObjectDisposedException(nameof(MockStreamStoreConnection));
                if (string.IsNullOrWhiteSpace(stream))
                    throw new ArgumentNullException(nameof(stream), $"{nameof(stream)} cannot be null or whitespace");

                var streamExists = _store.TryGetValue(stream, out var eventStream);
                if (streamExists && expectedVersion == ExpectedVersion.NoStream)
                    throw new WrongExpectedVersionException($"Stream {stream} exists, expected no stream");
                if (!streamExists && (expectedVersion == ExpectedVersion.StreamExists))
                    throw new WrongExpectedVersionException($"Stream {stream} does not exist, expected stream");

                if (!streamExists) {
                    eventStream = new List<RecordedEvent>();
                    _store.Add(stream, eventStream);
                }

                var startingPosition = eventStream.Count - 1;
                if (expectedVersion != ExpectedVersion.NoStream &&
                    expectedVersion != startingPosition)
                    throw new WrongExpectedVersionException(
                        $"Stream {stream} at position {eventStream.Count} expected {expectedVersion}.");
                var epochStart = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                for (var i = 0; i < events.Length; i++) {
                    var created = DateTime.UtcNow;
                    var epochTime = (long) (created - epochStart).TotalSeconds;
                    var recordedEvent = new RecordedEvent(
                        stream,
                        events[i].EventId,
                        eventStream.Count + 1,
                        events[i].EventType,
                        events[i].Data,
                        events[i].Metadata,
                        events[i].IsJson,
                        created,
                        epochTime);
                    eventStream.Add(recordedEvent);
                    _all.Add(recordedEvent);
                    _inboundEventHandler.Handle(new EventWritten(stream, recordedEvent));
                    //_inboundEventHandler.Handle(new ProjectedEventWritten(AllStreamName,recordedEvent));
                    //_inboundEventHandler.Handle(new ProjectedEventWritten(stream,recordedEvent));
                }

                return new WriteResult(eventStream.Count - 1);
            }
        }

        /// <summary>
        /// Generate the Category Stream
        /// </summary>
        /// <remarks>
        /// <see href="https://eventstore.org/docs/projections/system-projections/index.html"/>
        /// The category stream name is generated by spiting the event stream at the first dash. This is the default 
        /// configuration setting (prefix (a category) by splitting a stream id by a configurable separator.)
       /// </remarks>
        /// <param name="aggregateStream">Original event stream in the aggregate-GUID format</param>
        /// <returns>category StreamName</returns>
        private string GetOrCreateCategoryStream(string aggregateStream)
        {
            var category = aggregateStream.Split('-')[0];
            var categoryStreamName = $"{CategoryStreamNamePrefix}-{category}";

            lock (_accessLock) {
                if (!_store.TryGetValue(categoryStreamName, out var categoryStream)) {
                    categoryStream = new List<RecordedEvent>();
                    _store.Add(categoryStreamName, categoryStream);
                    _category = categoryStream;
                }
            }

            return categoryStreamName;
        }

        // ReSharper disable once UnusedMember.Local
        private string CreateEventTypeStream(string[] @event)
        {
            return EventTypeStreamNamePrefix + "-" + @event[0].GetType();
        }

        public StreamEventsSlice ReadStreamForward(string stream, long start, long count,
            UserCredentials credentials = null)
        {
            lock (_accessLock) {
                if (!_store.ContainsKey(stream)) throw new StreamNotFoundException(stream);
                var result = _store[stream].Skip((int) start).Take((int) count).ToArray();


                var slice = new StreamEventsSlice(
                    stream,
                    start,
                    ReadDirection.Forward,
                    result,
                    start + result.Length,
                    _store[stream].Count - 1,
                    start + result.Length == _store[stream].Count);
                return slice;
            }
        }

        public StreamEventsSlice ReadStreamBackward(string stream, long start, long count,
            UserCredentials credentials = null)
        {
            throw new NotImplementedException();
        }

        public IDisposable SubscribeToStream(
            string stream,
            Action<RecordedEvent> eventAppeared,
            Action<SubscriptionDropReason, Exception> subscriptionDropped = null,
            UserCredentials userCredentials = null)
        {
            lock (_accessLock) {
                if (!_store.ContainsKey(stream)) throw new StreamNotFoundException(stream);
                return SubscribeToStreamFrom(
                    stream,
                    _store[stream].Count - 1,
                    CatchUpSubscriptionSettings.Default,
                    eventAppeared,
                    null, //live processing started
                    subscriptionDropped,
                    userCredentials);
            }
        }

        public IDisposable SubscribeToStreamFrom(
            string stream,
            long? lastCheckpoint,
            CatchUpSubscriptionSettings settings,
            Action<RecordedEvent> eventAppeared,
            Action<Unit> liveProcessingStarted = null,
            Action<SubscriptionDropReason, Exception> subscriptionDropped = null,
            UserCredentials userCredentials = null)
        {
            lock (_accessLock) {
                if (!_store.ContainsKey(stream)) throw new StreamNotFoundException(stream);
                long live = 0;
                string streamName = stream;
                var live1 = live;
                var subscription = _inboundEventBus.Subscribe(new AdHocHandler<EventWritten>(@event => {
                    if (live1 != 0 && string.CompareOrdinal(streamName, @event.OriginalStream) == 0) {
                        eventAppeared(@event.Event);
                    }
                }));
                //n.b. this leaves a possible gap in the events at switchover, #mock-life
                var start = lastCheckpoint ?? 0;
                var curEvents = _store[stream].Skip((int) start).ToArray();
                while (curEvents.Length > 0) {
                    for (int i = 0; i < curEvents.Length; i++) {
                        eventAppeared(curEvents[i]);
                    }

                    start = start + curEvents.Length;
                    curEvents = _store[stream].Skip((int) start).ToArray();
                }

                Interlocked.Exchange(ref live, 1);
                liveProcessingStarted?.Invoke(Unit.Default);
                return subscription;
            }
        }

        public IDisposable SubscribeToAll(Action<RecordedEvent> eventAppeared,
            Action<SubscriptionDropReason, Exception> subscriptionDropped = null,
            UserCredentials userCredentials = null)
        {
            throw new NotImplementedException();
        }

        public void DeleteStream(string stream, int expectedVersion, UserCredentials credentials = null)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Write to the Category Stream
        /// </summary>
        /// <remarks>
        /// If the category stream doesn't exist, create it.
        /// The data in the event parameter includes the original stream, the recoreded event, and messageId.
        /// <seealso cref="EventWritten"/>
        /// Generate the name from the original stream. <see cref="GetOrCreateCategoryStream"/>
        /// The caller sets up the Category Stream, append the events."/>
        /// </remarks>
        /// <param name="event"></param>
        private void WriteToCategoryStream(EventWritten @event)
        {
            if (!_connected || _disposed)
                throw new Exception();
            var categoryStreamName = GetOrCreateCategoryStream(@event.OriginalStream);

            var epochStart = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var created = DateTime.UtcNow;
            var epochTime = (long)(created - epochStart).TotalSeconds;

            var projectedEvent = new ProjectedEvent(
                    categoryStreamName,
                    _category.Count + 1,
                    @event.Event.EventStreamId,
                    @event.Event.EventId, // reusing since the projection is linking to the original event
                    @event.Event.EventNumber,
                    @event.Event.EventType,
                    @event.Event.Data,
                    @event.Event.Metadata,
                    @event.Event.IsJson,
                    created,
                    epochTime);
            _category.Add(projectedEvent);
            _inboundEventHandler.Handle(new ProjectedEventWritten(projectedEvent));
        }

        private void WriteEventStream(EventWritten @event) {

        }

        private void PublishToSubscriptions(ProjectedEventWritten @event) {

        }

        public void Dispose() {
            Close();
            _inboundEventHandler?.Stop();
            _disposed = true;
        }
       
        public class EventWritten : Message {
            public readonly string OriginalStream;
            public readonly RecordedEvent Event;
            public EventWritten(
                string originalStream,
                RecordedEvent @event) {
                OriginalStream = originalStream;
                Event = @event;
            }
        }
        public class ProjectedEventWritten : Message {
            public readonly ProjectedEvent Event;
            public ProjectedEventWritten(
                ProjectedEvent @event) {
                Event = @event;
            }
        }
    }
}
