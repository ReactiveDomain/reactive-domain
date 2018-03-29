using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Xunit;

//The ES connection is async but everything here will be sync, (and we really don't want to party too hard)
#pragma warning disable 1998

namespace ReactiveDomain.Testing.EventStore
{
    public class TempTestHost
    {
        [Fact]
        public void can_create_read_result() {
            var rieType =
                Type.GetType(
                    "EventStore.ClientAPI.Messages.ClientMessage+ResolvedIndexedEvent,EventStore.ClientAPI", true);
            dynamic rie = Activator.CreateInstance(rieType,true);
            var readRslt = (EventReadResult) FormatterServices.GetUninitializedObject(typeof(EventReadResult));
            Assert.Null(readRslt.Event);
            
            ResolvedEvent? evt = new ResolvedEvent();
            typeof(EventReadResult)
                .GetField("Event")
                .SetValue(readRslt, evt);
            Assert.NotNull(readRslt.Event);
        }
    }

    /// <summary>
    /// Mock EventStore Connection
    /// provides simple in memory storage of events
    /// 
    /// Designed to support EventStoreRepository and Listener implementations
    /// for testing when other versions of the EventStore are not available
    /// See the InMemory EventStore and the docker container as first options
    /// 
    /// 
    /// Does not support (Not Implemented Exceptions are thrown for)
    ///     Transactions
    ///     Persistent Subscriptions
    ///     Stream MetaData
    ///     System Settings
    /// 
    /// User Authorization 
    ///     User Credentials are ignored and the operation always proceeds
    /// 
    /// Settings
    ///     Cannot apply settings 
    ///     Will always return default settings object
    /// 
    /// Deleted Streams 
    ///     Deleted Streams are permanently removed, we just delete the dictionary element
    ///     Will allow recreation of Deleted Streams, recreated stream will not contain previous events
    ///     Events will not be removed from the All Stream
    ///  
    /// </summary>
    public sealed class MockEventStoreConnection : IStreamStoreConnection
    {
        private bool _disposed;
        private bool _connected;

        private readonly Dictionary<StreamName, List<EventData>> _store;
        private readonly List<EventData> _all;
        private EventHandler<ClientConnectionEventArgs> _connected1;

        public ReadOnlyDictionary<StreamName, List<EventData>> Store => new ReadOnlyDictionary<StreamName, List<EventData>>(_store);
        public ReadOnlyCollection<EventData> All => new ReadOnlyCollection<EventData>(_all);

        public MockEventStoreConnection(string connectionName)
        {
            ConnectionName = connectionName;
           
            _store = new Dictionary<StreamName, List<EventData>>();
            _all = new List<EventData>();
        }

        public EventStoreStreamCatchUpSubscription SubscribeToStreamFrom(string stream, long? lastCheckpoint,
                                                                         CatchUpSubscriptionSettings settings, Action<EventStoreCatchUpSubscription, ResolvedEvent> eventAppeared,
                                                                         Action<EventStoreCatchUpSubscription> liveProcessingStarted = null,
                                                                         Action<EventStoreCatchUpSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
                                                                         UserCredentials userCredentials = null) {
            throw new NotImplementedException();
        }

        public async Task ConnectAsync()
        {
            _connected = true;
        }

        public void Close()
        {
            _connected = false;
        }

        EventHandler<ClientConnectionEventArgs> IStreamStoreConnection.Connected {
            get => _connected1;
            set => _connected1 = value;
        }

        private List<EventData> GetStream(StreamName stream, long expectedVersion)
        {
            var name = new StreamName(stream);
            _store.TryGetValue(stream, out var events);
            if (events == null) {
                if (expectedVersion == ExpectedVersion.Any ||
                    expectedVersion == ExpectedVersion.NoStream) {
                    return null;
                }

                throw new StreamNotFoundException(name);
            }

            if (expectedVersion == ExpectedVersion.Any ||
                expectedVersion == ExpectedVersion.StreamExists)
                return events;

            if (events.Count != expectedVersion)
                throw new WrongExpectedVersionException(
                    $"Stream {stream}, has version {events.Count}, expected version {expectedVersion}");
            return events;
        }

       

        #region Delete Stream
        
        /// Deleted Streams 
        ///     Deleted Streams are permanently removed, we just delete the dictionary element
        ///     Will allow recreation of Deleted Streams, recreated stream will not contain previous events
        ///     Events will not be removed from the All Stream
        public async Task<DeleteResult> DeleteStreamAsync(
                                            string stream,
                                            long expectedVersion,
                                            UserCredentials userCredentials = null) {
            var name = new StreamName(stream);
            var eventStream = GetStream(name, expectedVersion);
            _store.Remove(name);
            return new DeleteResult();
        }

        /// Deleted Streams 
        ///     Deleted Streams are permanently removed, we just delete the dictionary element
        ///     Will allow recreation of Deleted Streams, recreated stream will not contain previous events
        ///     Events will not be removed from the All Stream
        public async Task<DeleteResult> DeleteStreamAsync(
                                            string stream,
                                            long expectedVersion,
                                            bool hardDelete,
                                            UserCredentials userCredentials = null)
        {

            return DeleteStreamAsync(stream, expectedVersion).Result;
        }
        #endregion
        #region WriteToStream
        private WriteResult WriteEvents(StreamName name, long expectedVersion, IEnumerable<EventData> events) {
            try {
                var eventStream = GetStream(name, expectedVersion);
                if (eventStream == null) {
                    eventStream = new List<EventData>();
                    _store.Add(name, eventStream);
                }
            
                var eventsToWrite = events.ToArray();
                eventStream.AddRange(eventsToWrite);
                _all.AddRange(eventsToWrite);

                return new WriteResult(eventStream.Count, new Position(All.Count, All.Count));
            }
            catch (Exception) {
                throw;
            }
          
        }
        public async Task<WriteResult> AppendToStreamAsync(
                                        string stream,
                                        long expectedVersion,
                                        params EventData[] events) {

            return WriteEvents(new StreamName(stream), expectedVersion, events);
        }

        public async Task<EventStoreSubscription> SubscribeToStreamAsync(string stream, bool resolveLinkTos, Action<EventStoreSubscription, ResolvedEvent> eventAppeared, Action<EventStoreSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
                                                 UserCredentials userCredentials = null) {
            throw new NotImplementedException();
        }

        public async Task<WriteResult> AppendToStreamAsync(
                                        string stream,
                                        long expectedVersion,
                                        UserCredentials userCredentials,
                                        params EventData[] events)
        {
            return WriteEvents(new StreamName(stream), expectedVersion, events);
        }

        public async Task<WriteResult> AppendToStreamAsync(
                                        string stream,
                                        long expectedVersion,
                                        IEnumerable<EventData> events,
                                        UserCredentials userCredentials = null)
        {
            return WriteEvents(new StreamName(stream), expectedVersion, events);
        }
        #endregion
        public async Task<StreamEventsSlice> ReadStreamEventsForwardAsync(
                                                string stream,
                                                long start,
                                                int count,
                                                bool resolveLinkTos,
                                                UserCredentials userCredentials = null) {
            //var slice = new StreamEventsSlice();
            throw new NotImplementedException();
        }

        public async Task<StreamEventsSlice> ReadStreamEventsBackwardAsync(
                                                string stream,
                                                long start,
                                                int count,
                                                bool resolveLinkTos,
                                                UserCredentials userCredentials = null)
        {
            throw new NotImplementedException();
        }

        public async Task<StreamEventsSlice> ReadAllEventsForwardAsync(
                                            Position position,
                                            int maxCount,
                                            bool resolveLinkTos,
                                            UserCredentials userCredentials = null)
        {
            throw new NotImplementedException();
        }

        public async Task<StreamEventsSlice> ReadAllEventsBackwardAsync(
                                            Position position,
                                            int maxCount,
                                            bool resolveLinkTos,
                                            UserCredentials userCredentials = null)
        {
            throw new NotImplementedException();
        }

        public async Task<EventStoreSubscription> SubscribeToStreamAsync(
                                                    string stream,
                                                    bool resolveLinkTos,
                                                    Func<EventStoreSubscription, ResolvedEvent, Task> eventAppeared,
                                                    Action<EventStoreSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
                                                    UserCredentials userCredentials = null)
        {
            throw new NotImplementedException();
        }

        public EventStoreStreamCatchUpSubscription SubscribeToStreamFrom(
                                                    string stream,
                                                    long? lastCheckpoint,
                                                    bool resolveLinkTos,
                                                    Func<EventStoreCatchUpSubscription, ResolvedEvent, Task> eventAppeared,
                                                    Action<EventStoreCatchUpSubscription> liveProcessingStarted = null,
                                                    Action<EventStoreCatchUpSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
                                                    UserCredentials userCredentials = null,
                                                    int readBatchSize = 500,
                                                    string subscriptionName = "")
        {
            throw new NotImplementedException();
        }

        public EventStoreStreamCatchUpSubscription SubscribeToStreamFrom(
                                                    string stream,
                                                    long? lastCheckpoint,
                                                    CatchUpSubscriptionSettings settings,
                                                    Func<EventStoreCatchUpSubscription, ResolvedEvent, Task> eventAppeared,
                                                    Action<EventStoreCatchUpSubscription> liveProcessingStarted = null,
                                                    Action<EventStoreCatchUpSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
                                                    UserCredentials userCredentials = null)
        {
            throw new NotImplementedException();
        }

        public async Task<EventStoreSubscription> SubscribeToAllAsync(
                                                    bool resolveLinkTos,
                                                    Func<EventStoreSubscription, ResolvedEvent, Task> eventAppeared,
                                                    Action<EventStoreSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
                                                    UserCredentials userCredentials = null)
        {
            throw new NotImplementedException();
        }
        public EventStoreCatchUpSubscription SubscribeToAllFrom(
                                                    Position? lastCheckpoint,
                                                    bool resolveLinkTos,
                                                    Func<EventStoreCatchUpSubscription, ResolvedEvent, Task> eventAppeared,
                                                    Action<EventStoreCatchUpSubscription> liveProcessingStarted = null,
                                                    Action<EventStoreCatchUpSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
                                                    UserCredentials userCredentials = null,
                                                    int readBatchSize = 500,
                                                    string subscriptionName = "")
        {
            throw new NotImplementedException();
        }

        public EventStoreCatchUpSubscription SubscribeToAllFrom(
                                                    Position? lastCheckpoint,
                                                    CatchUpSubscriptionSettings settings,
                                                    Func<EventStoreCatchUpSubscription, ResolvedEvent, Task> eventAppeared,
                                                    Action<EventStoreCatchUpSubscription> liveProcessingStarted = null,
                                                    Action<EventStoreCatchUpSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
                                                    UserCredentials userCredentials = null)
        {
            throw new NotImplementedException();
        }
        public void Dispose()
        {
            _disposed = true;
            _connected = false;
        }
        //Transactions Not Implemented
        #region Transactions

        public async Task<EventStoreSubscription> SubscribeToAllAsync(bool resolveLinkTos, Action<EventStoreSubscription, ResolvedEvent> eventAppeared, Action<EventStoreSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
                                              UserCredentials userCredentials = null) {
            throw new NotImplementedException();
        }

        public async Task<EventStoreTransaction> StartTransactionAsync(
            string stream,
            long expectedVersion,
            UserCredentials userCredentials = null)
        {
            throw new NotImplementedException();
        }

        public async Task<DeleteResult> DeleteStreamAsync(StreamName stream, int expectedVersion) {
            throw new NotImplementedException();
        }

        public EventStoreTransaction ContinueTransaction(
            long transactionId,
            UserCredentials userCredentials = null)
        {
            throw new NotImplementedException();
        }
        #endregion
     
        public string ConnectionName { get; }
      
    }
}