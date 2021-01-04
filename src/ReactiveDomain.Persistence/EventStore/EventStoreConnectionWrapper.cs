using ReactiveDomain.Util;
using System;
using System.Linq;
using System.Threading.Tasks;
using EventStore.ClientAPI.Exceptions;
using ES = EventStore.ClientAPI;

namespace ReactiveDomain.EventStore
{
    public class EventStoreConnectionWrapper : IStreamStoreConnection
    {
        public readonly ES.IEventStoreConnection EsConnection;
        private bool _disposed;
        private const int WriteBatchSize = 500;

        public EventStoreConnectionWrapper(ES.IEventStoreConnection eventStoreConnection)
        {
            Ensure.NotNull(eventStoreConnection, nameof(eventStoreConnection));
            EsConnection = eventStoreConnection;
            //EsConnection.Connected += ConnOnConnected;
            //EsConnection.Disconnected += ConnOnDisconnected;
        }

        public event EventHandler<ClientConnectionEventArgs> Connected = (p1, p2) => { };
        public event EventHandler<ClientConnectionEventArgs> Disconnected = (p1, p2) => { };
        private void ConnOnDisconnected(object sender, ES.ClientConnectionEventArgs clientConnectionEventArgs)
        {
            Disconnected(sender, clientConnectionEventArgs.ToRdEventArgs(this));
        }
        
        private void ConnOnConnected(object sender, ES.ClientConnectionEventArgs clientConnectionEventArgs)
        {
            Connected(sender, clientConnectionEventArgs.ToRdEventArgs(this));
        }

        public string ConnectionName => EsConnection.ConnectionName;

        public void Connect()
        {
            try {
                EsConnection.ConnectAsync().Wait();
            }
            catch (AggregateException aggregate) {
                if (aggregate.InnerException is ES.Exceptions.CannotEstablishConnectionException) {
                    throw new CannotEstablishConnectionException(aggregate.InnerException.Message, aggregate.InnerException);
                }
                throw;
            }
        }

        public void Close()
        {
            EsConnection.Close();
        }


        public WriteResult AppendToStream(
                            string stream,
                            long expectedVersion,
                            UserCredentials credentials = null,
                            params EventData[] events)
        {
            try
            {
                if (events.Length < WriteBatchSize)
                {
                    return EsConnection.AppendToStreamAsync(stream, (int)expectedVersion, events.ToESEventData(), credentials.ToESCredentials()).Result.ToWriteResult();
                }

                var transaction = EsConnection.StartTransactionAsync(stream, (int)expectedVersion).Result;
                var position = 0;
                while (position < events.Length)
                {
                    var pageEvents = events.Skip(position).Take(WriteBatchSize).ToArray();
                    transaction.WriteAsync(pageEvents.ToESEventData()).Wait();
                    position += WriteBatchSize;
                }

                return transaction.CommitAsync().Result.ToWriteResult();
            }
            catch (AggregateException ex)
            {
                if (ex.InnerException is ES.Exceptions.WrongExpectedVersionException)
                {
                    throw new WrongExpectedVersionException(ex.InnerException.Message, ex.InnerException);
                }
                throw;
            }

        }

        public StreamEventsSlice ReadStreamForward(
                                    string stream,
                                    long start,
                                    long count,
                                    UserCredentials credentials = null)
        {
            //todo: why does this need an int with v 4.0 of eventstore?
            var slice = EsConnection.ReadStreamEventsForwardAsync(stream, (int)start, (int)count, true, credentials.ToESCredentials()).Result;
            switch (slice.Status)
            {
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
                                    UserCredentials credentials = null)
        {
            //todo: why does this need an int with v 4.0 of eventstore?
            var slice = EsConnection.ReadStreamEventsBackwardAsync(stream, (int)start, (int)count, true, credentials.ToESCredentials()).Result;
            switch (slice.Status)
            {
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

        public IDisposable SubscribeToStream(
                                    string stream,
                                    Action<RecordedEvent> eventAppeared,
                                    Action<SubscriptionDropReason, Exception> subscriptionDropped = null,
                                    UserCredentials userCredentials = null)
        {
            var sub = EsConnection.SubscribeToStreamAsync(
                                stream,
                                true,
                                async (_, evt) => { eventAppeared(evt.Event.ToRecordedEvent(evt.OriginalEvent.EventNumber)); await Task.FromResult(Unit.Default); },
                                (_, reason, ex) => subscriptionDropped?.Invoke((SubscriptionDropReason)(int)reason, ex),
                                userCredentials?.ToESCredentials()).Result;
            return new Disposer(() =>
            {
                sub?.Unsubscribe();
                sub?.Dispose();
                return Unit.Default;
            });
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
            var sub = EsConnection.SubscribeToStreamFrom(
                                            stream,
                                            (int?)lastCheckpoint,
                                            settings?.ToCatchUpSubscriptionSettings() ?? ES.CatchUpSubscriptionSettings.Default,
                                            async (_, evt) => { eventAppeared(evt.Event.ToRecordedEvent(evt.OriginalEvent.EventNumber)); await Task.FromResult(Unit.Default); },
                                            _ => liveProcessingStarted?.Invoke(Unit.Default),
                                            (_, reason, ex) => subscriptionDropped?.Invoke((SubscriptionDropReason)(int)reason, ex),
                                            userCredentials?.ToESCredentials());

            return new Disposer(() =>
            {
                sub?.Stop();
                return Unit.Default;
            });
        }

        public IDisposable SubscribeToAll(
            Action<RecordedEvent> eventAppeared,
            Action<SubscriptionDropReason, Exception> subscriptionDropped = null,
            UserCredentials userCredentials = null,
            bool resolveLinkTos = true)
        {
            var sub = EsConnection.SubscribeToAllAsync(
                resolveLinkTos,
                async (_, evt) =>
                {
                    eventAppeared(evt.Event.ToRecordedEvent());
                    await Task.FromResult(Unit.Default);
                },
                (_, reason, ex) => subscriptionDropped?.Invoke((SubscriptionDropReason)(int)reason, ex),
                userCredentials?.ToESCredentials()).Result;
            return new Disposer(() =>
            {
                sub?.Unsubscribe();
                sub?.Dispose();
                return Unit.Default;
            });
        }

        public IDisposable SubscribeToAllFrom(
            Position from,
            Action<RecordedEvent> eventAppeared,
            CatchUpSubscriptionSettings settings = null,
            Action liveProcessingStarted = null,
            Action<SubscriptionDropReason, Exception> subscriptionDropped = null,
            UserCredentials userCredentials = null,
            bool resolveLinkTos = true)
        {
            var sub = EsConnection.SubscribeToAllFrom(
                new ES.Position(from.CommitPosition, from.PreparePosition),
                settings?.ToCatchUpSubscriptionSettings(),
                async (_, evt) =>
                {
                    eventAppeared(evt.Event.ToRecordedEvent());
                    await Task.FromResult(Unit.Default);
                },
                __ => { liveProcessingStarted?.Invoke(); },
                (_, reason, ex) => subscriptionDropped?.Invoke((SubscriptionDropReason)(int)reason, ex),
                userCredentials?.ToESCredentials());
            return new Disposer(() =>
            {
                sub.Stop(TimeSpan.FromMilliseconds(250));
                return Unit.Default;
            });
        }


        public void DeleteStream(string stream, int expectedVersion, UserCredentials credentials = null)
                        => EsConnection.DeleteStreamAsync(stream, expectedVersion, credentials.ToESCredentials()).Wait();
        public void Dispose()
        {
            if (!_disposed)
            {
                if (EsConnection != null)
                {
                    EsConnection.Close();
                    EsConnection.Connected -= ConnOnConnected;
                    EsConnection.Disconnected -= ConnOnDisconnected;
                    EsConnection.Dispose();
                }
            }
            _disposed = true;
        }
    }

    public static class ConnectionHelpers
    {
        public static WriteResult ToWriteResult(this ES.WriteResult result)
        {
            return new WriteResult(result.NextExpectedVersion);
        }
        public static ES.SystemData.UserCredentials ToESCredentials(this UserCredentials credentials)
        {
            return credentials == null ?
                null :
                new ES.SystemData.UserCredentials(credentials.Username, credentials.Password);
        }

        public static ClientConnectionEventArgs ToRdEventArgs(this ES.ClientConnectionEventArgs args, IStreamStoreConnection conn)
        {
            return new ClientConnectionEventArgs(conn, args.RemoteEndPoint);
        }

        public static StreamEventsSlice ToStreamEventsSlice(this ES.StreamEventsSlice slice)
        {
            return new StreamEventsSlice(
                    slice.Stream,
                    slice.FromEventNumber,
                    slice.ReadDirection.ToReadDirection(),
                    slice.Events.ToRecordedEvents(),
                    slice.NextEventNumber,
                    slice.LastEventNumber,
                    slice.IsEndOfStream);
        }

        public static RecordedEvent[] ToRecordedEvents(this ES.ResolvedEvent[] resolvedEvents)
        {
            Ensure.NotNull(resolvedEvents, nameof(resolvedEvents));
            var events = new RecordedEvent[resolvedEvents.Length];
            for (int i = 0; i < resolvedEvents.Length; i++)
            {
                var evt = resolvedEvents[i].Event;
                //we want the event number from the stream we're reading not the original stream
                events[i] = evt.ToRecordedEvent(resolvedEvents[i].OriginalEvent.EventNumber);
            }
            return events;
        }

        public static RecordedEvent ToRecordedEvent(this ES.RecordedEvent recordedEvent, long? eventNumber = null)
        {
            return new RecordedEvent(
                recordedEvent.EventStreamId,
                recordedEvent.EventId,
                eventNumber ?? recordedEvent.EventNumber,
                recordedEvent.EventType,
                recordedEvent.Data,
                recordedEvent.Metadata,
                recordedEvent.IsJson,
                recordedEvent.Created,
                recordedEvent.CreatedEpoch);
        }

        public static ES.EventData[] ToESEventData(this EventData[] events)
        {
            Ensure.NotNull(events, nameof(events));
            var result = new ES.EventData[events.Length];
            for (int i = 0; i < events.Length; i++)
            {
                result[i] = events[i].ToESEventData();
            }
            return result;
        }
        public static ES.EventData ToESEventData(this EventData @event)
        {
            Ensure.NotNull(@event, nameof(@event));
            return new ES.EventData(
                            @event.EventId,
                            @event.EventType,
                            @event.IsJson,
                            @event.Data,
                            @event.Metadata);
        }

        public static ReadDirection ToReadDirection(this ES.ReadDirection readDirection)
        {
            switch (readDirection)
            {
                case ES.ReadDirection.Forward:
                    return ReadDirection.Forward;
                case ES.ReadDirection.Backward:
                    return ReadDirection.Backward;
                default:
                    throw new ArgumentOutOfRangeException(nameof(readDirection), "Unknown ReadDirection returned from Eventstore");
            }
        }

        public static ES.CatchUpSubscriptionSettings ToCatchUpSubscriptionSettings(this CatchUpSubscriptionSettings settings)
        {
            if (null == settings)
                return null;
#if NET452
            return new ES.CatchUpSubscriptionSettings(
                settings.MaxLiveQueueSize,
                settings.ReadBatchSize,
                settings.VerboseLogging,
                true
            );
#else
            return new ES.CatchUpSubscriptionSettings(
                settings.MaxLiveQueueSize,
                settings.ReadBatchSize,
                settings.VerboseLogging,
                true,
                settings.SubscriptionName
            );
#endif
        }
    }
}