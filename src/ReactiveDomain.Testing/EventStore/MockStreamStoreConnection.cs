using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReactiveDomain.Foundation.EventStore;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ReactiveDomain.Foundation;

// ReSharper disable MemberCanBePrivate.Global
namespace ReactiveDomain.Testing.EventStore {
    public class MockStreamStoreConnection : IStreamStoreConnection {


        private readonly Dictionary<string, List<EventData>> _store;
        private readonly List<EventData> _all;
        public ReadOnlyDictionary<string, List<EventData>> Store => new ReadOnlyDictionary<string, List<EventData>>(_store);
        public ReadOnlyCollection<EventData> All => _all.AsReadOnly();
        private bool _connected;
        private bool _disposed;


        public MockStreamStoreConnection() {
            _all = new List<EventData>();
            _store = new Dictionary<string, List<EventData>>();
        }


        public string ConnectionName { get; }

        public void Connect() {
            _connected = true;
        }
        public void Close() {
            _connected = false;
        }

        public event EventHandler<ClientConnectionEventArgs> Connected = (p1,p2)=> { };

        public WriteResult AppendToStream(string stream, long expectedVersion, UserCredentials credentials = null,
                                          params EventData[] events) {
            throw new NotImplementedException();
        }

        public StreamEventsSlice ReadStreamForward(string stream, long start, long count, UserCredentials credentials = null) {
            throw new NotImplementedException();
        }

        public StreamEventsSlice ReadStreamBackward(string stream, long start, long count, UserCredentials credentials = null) {
            throw new NotImplementedException();
        }

        public StreamSubscription SubscribeToStream(string stream, Action<StreamSubscription, ReactiveDomain.RecordedEvent> eventAppeared, Action<StreamSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
                                                    UserCredentials userCredentials = null) {
            throw new NotImplementedException();
        }

        public StreamCatchUpSubscription SubscribeToStreamFrom(string stream, long? lastCheckpoint,
                                                               CatchUpSubscriptionSettings settings, Action<CatchUpSubscription, ReactiveDomain.RecordedEvent> eventAppeared,
                                                               Action<CatchUpSubscription> liveProcessingStarted = null, Action<CatchUpSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
                                                               UserCredentials userCredentials = null) {
            throw new NotImplementedException();
        }

        public StreamSubscription SubscribeToAll(Action<StreamSubscription, ReactiveDomain.RecordedEvent> eventAppeared, Action<StreamSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
                                                 UserCredentials userCredentials = null) {
            throw new NotImplementedException();
        }

        public StreamSubscription SubscribeToAllFrom(long? lastCheckpoint, CatchUpSubscriptionSettings settings, Action<CatchUpSubscription, ReactiveDomain.RecordedEvent> eventAppeared,
                                                     Action<CatchUpSubscription> liveProcessingStarted = null, Action<CatchUpSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
                                                     UserCredentials userCredentials = null) {
            throw new NotImplementedException();
        }

        public void DeleteStream(StreamName stream, int expectedVersion, UserCredentials credentials = null) {
            throw new NotImplementedException();
        }

        public void Dispose() {
            Close();
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






        #region StreamStoreConnection

       
        #endregion
    }
}
