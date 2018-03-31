using System;
using System.Threading;
using System.Threading.Tasks;
using ReactiveDomain.Logging;
using ReactiveDomain.Util;

namespace ReactiveDomain
{
   public class StreamCatchUpSubscription : CatchUpSubscription
  {
    private long _nextReadEventNumber;
    private long _lastProcessedEventNumber;

    /// <summary>The last event number processed on the subscription.</summary>
    public long LastProcessedEventNumber => _lastProcessedEventNumber;

      internal StreamCatchUpSubscription(IStreamStoreConnection connection, ILogger log, string streamId, long? fromEventNumberExclusive, UserCredentials userCredentials, Func<CatchUpSubscription, RecordedEvent, Task> eventAppeared, Action<CatchUpSubscription> liveProcessingStarted, Action<CatchUpSubscription, SubscriptionDropReason, Exception> subscriptionDropped, CatchUpSubscriptionSettings settings)
      : base(connection, log, streamId, userCredentials, eventAppeared, liveProcessingStarted, subscriptionDropped, settings)
    {
      Ensure.NotNullOrEmpty(streamId, nameof (streamId));
      long? nullable = fromEventNumberExclusive;
      _lastProcessedEventNumber = nullable ?? -1L;
      nullable = fromEventNumberExclusive;
      _nextReadEventNumber = nullable ?? 0L;
    }

    /// <summary>Read events until the given event number async.</summary>
    /// <param name="connection">The connection.</param>
    /// <param name="userCredentials">User credentials for the operation.</param>
    /// <param name="lastCommitPosition">The commit position to read until.</param>
    /// <param name="lastEventNumber">The event number to read until.</param>
    /// <returns>
    /// </returns>
    protected override Task ReadEventsTillAsync(IStreamStoreConnection connection, UserCredentials userCredentials, long? lastCommitPosition, long? lastEventNumber)
    {
      return ReadEventsInternalAsync(connection,  userCredentials, lastEventNumber);
    }

    private async Task ReadEventsInternalAsync(IStreamStoreConnection connection,  UserCredentials userCredentials, long? lastEventNumber)
    {
      StreamEventsSlice slice;
      do
      {
        slice = connection.ReadStreamForward(
                            StreamId, 
                            _nextReadEventNumber, 
                            ReadBatchSize,  
                            userCredentials);
      }
      while (!await ReadEventsCallbackAsync(slice, lastEventNumber).ConfigureAwait(false));
    }

    private async Task<bool> ReadEventsCallbackAsync(StreamEventsSlice slice, long? lastEventNumber)
    {
      bool flag1 = ShouldStop;
      if (!flag1)
        flag1 = await ProcessEventsAsync(lastEventNumber, slice).ConfigureAwait(false);
      bool flag2 = flag1;
      if (flag2 && Verbose)
        Log.Debug("Catch-up Subscription {0} to {1}: finished reading events, nextReadEventNumber = {2}.", (object) SubscriptionName, IsSubscribedToAll ? (object) "<all>" : (object) StreamId, (object) _nextReadEventNumber);
      return flag2;
    }

    private async Task<bool> ProcessEventsAsync(long? lastEventNumber, StreamEventsSlice slice)
    {
      bool done;
        switch (slice) {
            case StreamDeletedSlice _:
                throw new StreamDeletedException( new StreamName(StreamId)); 
            case StreamNotFoundSlice _:
                if (lastEventNumber == -1)
                    throw new Exception(
                        $"Impossible: stream {StreamId} disappeared in the middle of catching up subscription {SubscriptionName}.");
                done = true;
                break;
            case StreamEventsSlice _:
                RecordedEvent[] resolvedEventArray = slice.Events;
                for (int index = 0; index < resolvedEventArray.Length; ++index)
                    await TryProcessAsync(resolvedEventArray[index]).ConfigureAwait(false);
                
                _nextReadEventNumber = slice.NextEventNumber;
                if (lastEventNumber.HasValue){
                    done = slice.NextEventNumber > lastEventNumber.Value;
                }
                else
                    done = slice.IsEndOfStream;
                break;
            default:
                throw new ArgumentOutOfRangeException(string.Format($"Subscription {SubscriptionName} unexpected StreamEventsSlice.Status."));
        }
      
      if (!done && slice.IsEndOfStream)
        Thread.Sleep(1);
      return done;
    }

    /// <summary>
    /// Try to process a single <see cref="T:EventStore.ClientAPI.ResolvedEvent" />.
    /// </summary>
    /// <param name="e">The <see cref="T:EventStore.ClientAPI.ResolvedEvent" /> to process.</param>
    protected override async Task TryProcessAsync(RecordedEvent e)
    {
      bool flag = false;
      if (e.EventNumber > _lastProcessedEventNumber)
      {
        try
        {
          await EventAppeared(this, e).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
          DropSubscription(SubscriptionDropReason.EventHandlerException, ex);
          throw;
        }
        _lastProcessedEventNumber = e.EventNumber;
        flag = true;
      }
      if (!Verbose)
        return;
      Log.Debug("Catch-up Subscription {0} to {1}: {2} event ({3}, {4}, {5} @ {6}).", SubscriptionName, IsSubscribedToAll ?  "<all>" : StreamId, flag ? "processed" :"skipping",  e.EventStreamId,  e.EventNumber, e.EventType,  e.EventNumber);
    }
  }
}
