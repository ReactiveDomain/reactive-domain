using System;
using System.Linq;
using ReactiveDomain.Util;
using ES = EventStore.ClientAPI;

namespace ReactiveDomain.EventStore {
    public class EventStoreConnectionWrapper : IStreamStoreConnection {

        private readonly ES.IEventStoreConnection _conn;
        private bool _disposed;
        private const int WriteBatchSize = 500;

        public EventStoreConnectionWrapper(ES.IEventStoreConnection eventStoreConnection) {
            Ensure.NotNull(eventStoreConnection, nameof(eventStoreConnection));
            _conn = eventStoreConnection;
            _conn.Connected += ConnOnConnected;
        }

        public event EventHandler<ClientConnectionEventArgs> Connected = (p1, p2) => { };
        private void ConnOnConnected(object sender, ES.ClientConnectionEventArgs clientConnectionEventArgs) {
            Connected(sender, clientConnectionEventArgs.ToRdEventArgs(this));
        }

        public string ConnectionName => _conn.ConnectionName;

        public void Connect() {
            _conn.ConnectAsync().Wait();
        }

        public void Close() {
            _conn.Close();
        }

        
        public WriteResult AppendToStream(
                            string stream, 
                            long expectedVersion, 
                            UserCredentials credentials = null, 
                            params EventData[] events) {
            if (events.Length < WriteBatchSize) {
                return _conn.AppendToStreamAsync(stream, (int)expectedVersion, events.ToESEventData(), credentials.ToESCredentials()).Result.ToWriteResult();
            }
            else {
                var transaction = _conn.StartTransactionAsync(stream, (int)expectedVersion).Result;

                var position = 0;
                while (position < events.Length) {
                    var pageEvents = events.Skip(position).Take(WriteBatchSize).ToArray();
                    transaction.WriteAsync(pageEvents.ToESEventData()).Wait();
                    position += WriteBatchSize;
                }
               return transaction.CommitAsync().Result.ToWriteResult();
            }
        }

        public StreamEventsSlice ReadStreamForward(
                                    string stream,
                                    long start,
                                    long count,
                                    UserCredentials credentials = null) {
            //todo: why does this need an int with v 4.0 of eventstore?
            var slice = _conn.ReadStreamEventsForwardAsync(stream, (int)start, (int)count, true, credentials.ToESCredentials()).Result;
            switch (slice.Status) {
                case ES.SliceReadStatus.Success:
                    return slice.ToStreamEventsSlice();
                case ES.SliceReadStatus.StreamNotFound:
                    return new StreamNotFoundSlice(slice.Stream);
                case ES.SliceReadStatus.StreamDeleted:
                    return new StreamDeletedSlice(slice.Stream);
                default:
                    throw new ArgumentOutOfRangeException(nameof(slice.Status), "Unknown read status returned from IEventStoreConnection ReadStreamEventsForwardAsync");
            }


        }
        public StreamEventsSlice ReadStreamBackward(
                                    string stream,
                                    long start,
                                    long count,
                                    UserCredentials credentials = null) {
            //todo: why does this need an int with v 4.0 of eventstore?
            var slice = _conn.ReadStreamEventsBackwardAsync(stream, (int)start, (int)count, true, credentials.ToESCredentials()).Result;
            switch (slice.Status) {
                case ES.SliceReadStatus.Success:
                    return slice.ToStreamEventsSlice();
                case ES.SliceReadStatus.StreamNotFound:
                    return new StreamNotFoundSlice(slice.Stream);
                case ES.SliceReadStatus.StreamDeleted:
                    return new StreamDeletedSlice(slice.Stream);
                default:
                    throw new ArgumentOutOfRangeException(nameof(slice.Status), "Unknown read status returned from IEventStoreConnection ReadStreamEventsBackwardAsync");
            }
        }



        public StreamSubscription SubscribeToStream(
                                    string stream,
                                    Action<StreamSubscription, RecordedEvent> eventAppeared,
                                    Action<StreamSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
                                    UserCredentials userCredentials = null) {
            throw new NotImplementedException();
        }

        public StreamCatchUpSubscription SubscribeToStreamFrom(
                                            string stream,
                                            long? lastCheckpoint,
                                            CatchUpSubscriptionSettings settings,
                                            Action<CatchUpSubscription, RecordedEvent> eventAppeared,
                                            Action<CatchUpSubscription> liveProcessingStarted = null,
                                            Action<CatchUpSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
                                            UserCredentials userCredentials = null) {
            throw new NotImplementedException();
        }

        public StreamSubscription SubscribeToAll(
                                    Action<StreamSubscription, RecordedEvent> eventAppeared,
                                    Action<StreamSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
                                    UserCredentials userCredentials = null) {
            throw new NotImplementedException();
        }
        public StreamSubscription SubscribeToAllFrom(
                                    long? lastCheckpoint,
                                    CatchUpSubscriptionSettings settings,
                                    Action<CatchUpSubscription, RecordedEvent> eventAppeared,
                                    Action<CatchUpSubscription> liveProcessingStarted = null,
                                    Action<CatchUpSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
                                    UserCredentials userCredentials = null) {
            throw new NotImplementedException();
        }


        public void DeleteStream(StreamName stream, int expectedVersion, UserCredentials credentials = null)
                        => _conn.DeleteStreamAsync(stream, expectedVersion, credentials.ToESCredentials()).Wait();
        public void Dispose() {
            if (!_disposed) {
                if (_conn != null) {
                    _conn.Close();
                    _conn.Connected -= ConnOnConnected;
                    _conn.Dispose();
                }
            }
            _disposed = true;
        }
    }

    public static class ConnectionHelpers {
        public static WriteResult ToWriteResult(this ES.WriteResult result) {
            return new WriteResult(result.NextExpectedVersion);
        }
        public static ES.SystemData.UserCredentials ToESCredentials(this UserCredentials credentials) {
            return credentials == null ? 
                null : 
                new ES.SystemData.UserCredentials(credentials.Username, credentials.Password);
        }

        public static ClientConnectionEventArgs ToRdEventArgs(this ES.ClientConnectionEventArgs args, IStreamStoreConnection conn) {
            return new ClientConnectionEventArgs(conn, args.RemoteEndPoint);
        }

        public static StreamEventsSlice ToStreamEventsSlice(this ES.StreamEventsSlice slice) {
            return new StreamEventsSlice(
                    slice.Stream,
                    slice.FromEventNumber,
                    slice.ReadDirection.ToReadDirection(),
                    slice.Events.ToRecordedEvents(),
                    slice.NextEventNumber,
                    slice.LastEventNumber,
                    slice.IsEndOfStream);
        }

        public static RecordedEvent[] ToRecordedEvents(this ES.ResolvedEvent[] resolvedEvents) {
            Ensure.NotNull(resolvedEvents, nameof(resolvedEvents));
            var events = new RecordedEvent[resolvedEvents.Length];
            for (int i = 0; i < resolvedEvents.Length; i++) {
                events[i] = resolvedEvents[i].Event.ToRecordedEvent();
            }
            return events;
        }

        public static RecordedEvent ToRecordedEvent(this ES.RecordedEvent recordedEvent) {
            return new RecordedEvent(
                recordedEvent.EventStreamId,
                recordedEvent.EventId,
                recordedEvent.EventNumber,
                recordedEvent.EventType,
                recordedEvent.Data,
                recordedEvent.Metadata,
                recordedEvent.IsJson,
                recordedEvent.Created,
                recordedEvent.CreatedEpoch);
        }
        public static ES.EventData[] ToESEventData(this EventData[] events) {
            Ensure.NotNull(events, nameof(events));
            var result = new ES.EventData[events.Length];
            for (int i = 0; i < events.Length; i++) {
                result[i] = events[i].ToESEventData();
            }
            return result;
        }
        public static ES.EventData ToESEventData(this EventData @event) {
            Ensure.NotNull(@event, nameof(@event));
            return new ES.EventData(
                            @event.EventId,
                            @event.Type,
                            @event.IsJson,
                            @event.Data,
                            @event.Metadata);
        }

        public static ReadDirection ToReadDirection(this ES.ReadDirection readDirection) {
            switch (readDirection) {
                case ES.ReadDirection.Forward:
                    return ReadDirection.Forward;
                case ES.ReadDirection.Backward:
                    return ReadDirection.Backward;
                default:
                    throw new ArgumentOutOfRangeException(nameof(readDirection), "Unknown ReadDirection returned from Eventstore");
            }
        }

    }
}