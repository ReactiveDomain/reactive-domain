using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ReactiveDomain.Util;

// ReSharper disable MemberCanBePrivate.Global
namespace ReactiveDomain.Testing.EventStore {
    public class MockStreamStoreConnection : IStreamStoreConnection {

        public const string CategoryStreamNamePrefix = @"$ce";
        public const string EventTypeStreamNamePrefix = @"$et";
        public const string AllStreamName = @"All";
        private readonly Dictionary<string, List<RecordedEvent>> _store;
        private readonly List<RecordedEvent> _all;
        private readonly QueuedHandler _inboundEventHandler;
        private readonly IBus _inboundEventBus;
        private bool _connected;
        private bool _disposed;

        public MockStreamStoreConnection(string name) {
            _all = new List<RecordedEvent>();
            _store = new Dictionary<string, List<RecordedEvent>>();
            _inboundEventBus = new InMemoryBus(nameof(_inboundEventBus), false);
            _inboundEventBus.Subscribe(new AdHocHandler<EventWritten>(WriteCategoryStream));
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
            if (!_connected || _disposed)
                throw new Exception();
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
            var startingPosition = eventStream.Count;
            if (expectedVersion != ExpectedVersion.NoStream &&
                expectedVersion != startingPosition)
                throw new WrongExpectedVersionException($"Stream {stream} at position {eventStream.Count} expected {expectedVersion}.");
            var epochStart = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            for (var i = 0; i < events.Length; i++) {
                var created = DateTime.UtcNow;
                var epochTime = (long)(created - epochStart).TotalSeconds;
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


        public StreamEventsSlice ReadStreamForward(string stream, long start, long count, UserCredentials credentials = null) {
            if (!_store.ContainsKey(stream)) throw new StreamNotFoundException(stream);
            var result = _store[stream].Skip((int)start).Take((int)count).ToArray();


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

        public StreamEventsSlice ReadStreamBackward(string stream, long start, long count, UserCredentials credentials = null) {
            throw new NotImplementedException();
        }

        public IDisposable SubscribeToStream(
            string stream,
            Action<ReactiveDomain.RecordedEvent> eventAppeared,
            Action<SubscriptionDropReason, Exception> subscriptionDropped = null,
            UserCredentials userCredentials = null) {
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

        public IDisposable SubscribeToStreamFrom(
            string stream,
            long? lastCheckpoint,
            CatchUpSubscriptionSettings settings,
            Action<RecordedEvent> eventAppeared,
            Action<Unit> liveProcessingStarted = null,
            Action<SubscriptionDropReason, Exception> subscriptionDropped = null,
            UserCredentials userCredentials = null) {
            if (!_store.ContainsKey(stream)) throw new StreamNotFoundException(stream);
            long _live = 0;
            string streamName = stream;
            var subscription = _inboundEventBus.Subscribe(new AdHocHandler<EventWritten>(@event => {
                if (_live != 0 && string.CompareOrdinal(streamName, @event.OriginalStream) == 0) {
                    eventAppeared(@event.Event);
                }
            }));
            //n.b. this leaves a possible gap in the events at switchover, #mock-life
            var start = lastCheckpoint ?? 0;
            var curEvents = _store[stream].Skip((int)start).ToArray();
            while (curEvents.Length > 0) {
                for (int i = 0; i < curEvents.Length; i++) {
                    eventAppeared(curEvents[i]);
                }
                start = start + curEvents.Length;
                curEvents = _store[stream].Skip((int)start).ToArray();
            }
            Interlocked.Exchange(ref _live, 1);
            liveProcessingStarted?.Invoke(Unit.Default);
            return subscription;
        }

        public IDisposable SubscribeToAll(Action<ReactiveDomain.RecordedEvent> eventAppeared, Action<SubscriptionDropReason, Exception> subscriptionDropped = null,
                                                 UserCredentials userCredentials = null) {
            throw new NotImplementedException();
        }

        public void DeleteStream(string stream, int expectedVersion, UserCredentials credentials = null) {
            throw new NotImplementedException();
        }
        private void WriteCategoryStream(EventWritten @event) {

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
        //public TAggregate GetById<TAggregate>(Guid id, int version) where TAggregate : class, IEventSource
        //{
        //    if (version <= 0)
        //        throw new InvalidOperationException("Cannot get version <= 0");

        //    var streamName = _streamNameBuilder.GenerateForAggregate(typeof(TAggregate), id);
        //    var aggregate = ConstructAggregate<TAggregate>();

        //    if (!_store.TryGetValue(streamName, out var stream))
        //        throw new AggregateNotFoundException(id, typeof(TAggregate));

        //    var events = new List<Object>();
        //    foreach (var @event in stream.Take(version))
        //        events.Add(DeserializeEvent(@event.Metadata, @event.Data));

        //    aggregate.RestoreFromEvents(events);

        //    if (version != Int32.MaxValue && aggregate.ExpectedVersion != version - 1)
        //        throw new AggregateVersionException(id, typeof(TAggregate), version, aggregate.ExpectedVersion);

        //    return aggregate;
        //}



        //public void Save(IEventSource aggregate)
        //{
        //    var commitHeaders = new Dictionary<string, object>
        //    {
        //        {CommitIdHeader, Guid.NewGuid() /*commitId*/},
        //        {AggregateClrTypeHeader, aggregate.GetType().AssemblyQualifiedName}
        //    };

        //    var streamName = _streamNameBuilder.GenerateForAggregate(aggregate.GetType(), aggregate.Id);
        //    var categoryStreamName = _streamNameBuilder.GenerateForCategory(aggregate.GetType());
        //    var newEvents = aggregate.TakeEvents().ToList();

        //    var eventsToSave = newEvents.Select(e => ToEventData(Guid.NewGuid(), e, commitHeaders)).ToList();

        //    _store.TryGetValue(streamName, out var stream);
        //    _store.TryGetValue(categoryStreamName, out var catStream);



        //    if (stream == null)
        //    {
        //        if (aggregate.ExpectedVersion == ExpectedVersion.Any || aggregate.ExpectedVersion == ExpectedVersion.NoStream)
        //        {
        //            stream = new List<EventData>();
        //            _store.Add(streamName, stream);
        //            if (catStream == null)
        //            {
        //                catStream = new List<EventData>();
        //                _store.Add(categoryStreamName, catStream);
        //            }
        //        }
        //        else throw new WrongExpectedVersionException("Stream " + streamName + " does not exist.");
        //    }

        //    if (stream.Count != 0 && stream.Count - 1 != aggregate.ExpectedVersion) // a new stream will be @ version 0 
        //        throw new AggregateException(new WrongExpectedVersionException(
        //            $"Stream {streamName} at version {stream.Count}, expected version {aggregate.ExpectedVersion}"));


        //    stream.AddRange(eventsToSave);
        //    catStream?.AddRange(eventsToSave);

        //    foreach (var evt in eventsToSave)
        //    {
        //        var etName = _streamNameBuilder.GenerateForEventType(evt.Type);
        //        if (!_store.TryGetValue(etName, out var etStream))
        //        {
        //            etStream = new List<EventData>();
        //            _store.Add(etName, etStream);
        //        }

        //        etStream.Add(evt);
        //    }

        //    foreach (var @event in newEvents.Where(@event => (@event as Message) != null))
        //    {
        //        _postCommitTarget?.Publish(@event as Message);
        //        _history.Add(new Tuple<string, Message>(streamName, @event as Message));
        //    }
        //}

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
