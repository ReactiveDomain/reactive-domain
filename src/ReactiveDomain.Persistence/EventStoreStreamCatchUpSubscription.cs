using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ReactiveDomain.Logging;
using ReactiveDomain.Util;

namespace ReactiveDomain
{
   public class EventStoreStreamCatchUpSubscription : EventStoreCatchUpSubscription
  {
    private long _nextReadEventNumber;
    private long _lastProcessedEventNumber;

    /// <summary>The last event number processed on the subscription.</summary>
    public long LastProcessedEventNumber => this._lastProcessedEventNumber;

      internal EventStoreStreamCatchUpSubscription(IStreamStoreConnection connection, ILogger log, string streamId, long? fromEventNumberExclusive, UserCredentials userCredentials, Func<EventStoreCatchUpSubscription, ResolvedEvent, Task> eventAppeared, Action<EventStoreCatchUpSubscription> liveProcessingStarted, Action<EventStoreCatchUpSubscription, SubscriptionDropReason, Exception> subscriptionDropped, CatchUpSubscriptionSettings settings)
      : base(connection, log, streamId, userCredentials, eventAppeared, liveProcessingStarted, subscriptionDropped, settings)
    {
      Ensure.NotNullOrEmpty(streamId, nameof (streamId));
      long? nullable = fromEventNumberExclusive;
      this._lastProcessedEventNumber = nullable ?? -1L;
      nullable = fromEventNumberExclusive;
      this._nextReadEventNumber = nullable ?? 0L;
    }

    /// <summary>Read events until the given event number async.</summary>
    /// <param name="connection">The connection.</param>
    /// <param name="resolveLinkTos">Whether to resolve Link events.</param>
    /// <param name="userCredentials">User credentials for the operation.</param>
    /// <param name="lastCommitPosition">The commit position to read until.</param>
    /// <param name="lastEventNumber">The event number to read until.</param>
    /// <returns>
    /// </returns>
    protected override Task ReadEventsTillAsync(IStreamStoreConnection connection, bool resolveLinkTos, UserCredentials userCredentials, long? lastCommitPosition, long? lastEventNumber)
    {
      return this.ReadEventsInternalAsync(connection, resolveLinkTos, userCredentials, lastEventNumber);
    }

    private async Task ReadEventsInternalAsync(IStreamStoreConnection connection, bool resolveLinkTos, UserCredentials userCredentials, long? lastEventNumber)
    {
      StreamEventsSlice slice;
      do
      {
        slice = await connection.ReadStreamEventsForwardAsync(
                            StreamId, 
                            _nextReadEventNumber, 
                            ReadBatchSize, 
                            resolveLinkTos, 
                            userCredentials).ConfigureAwait(false);
      }
      while (!await this.ReadEventsCallbackAsync(slice, lastEventNumber).ConfigureAwait(false));
    }

    private async Task<bool> ReadEventsCallbackAsync(StreamEventsSlice slice, long? lastEventNumber)
    {
      bool flag1 = this.ShouldStop;
      if (!flag1)
        flag1 = await this.ProcessEventsAsync(lastEventNumber, slice).ConfigureAwait(false);
      bool flag2 = flag1;
      if (flag2 && this.Verbose)
        this.Log.Debug("Catch-up Subscription {0} to {1}: finished reading events, nextReadEventNumber = {2}.", (object) this.SubscriptionName, this.IsSubscribedToAll ? (object) "<all>" : (object) this.StreamId, (object) this._nextReadEventNumber);
      return flag2;
    }

    private async Task<bool> ProcessEventsAsync(long? lastEventNumber, StreamEventsSlice slice)
    {
      bool done;
      switch (slice.Status)
      {
        case SliceReadStatus.Success:
          ResolvedEvent[] resolvedEventArray = slice.Events;
          for (int index = 0; index < resolvedEventArray.Length; ++index)
            await this.TryProcessAsync(resolvedEventArray[index]).ConfigureAwait(false);
          resolvedEventArray = (ResolvedEvent[]) null;
          this._nextReadEventNumber = slice.NextEventNumber;
          int num1;
          if (lastEventNumber.HasValue)
          {
            long nextEventNumber = slice.NextEventNumber;
            long? nullable = lastEventNumber;
            long valueOrDefault = nullable.GetValueOrDefault();
            num1 = nextEventNumber > valueOrDefault ? (nullable.HasValue ? 1 : 0) : 0;
          }
          else
            num1 = slice.IsEndOfStream ? 1 : 0;
          done = num1 != 0;
          break;
        case SliceReadStatus.StreamNotFound:
          if (lastEventNumber.HasValue)
          {
            long? nullable = lastEventNumber;
            long num2 = -1;
            if ((nullable.GetValueOrDefault() == num2 ? (!nullable.HasValue ? 1 : 0) : 1) != 0)
              throw new Exception(string.Format("Impossible: stream {0} disappeared in the middle of catching up subscription {1}.", (object) this.StreamId, (object) this.SubscriptionName));
          }
          done = true;
          break;
        case SliceReadStatus.StreamDeleted:
          throw new StreamDeletedException( new StreamName(StreamId));
        default:
          throw new ArgumentOutOfRangeException(string.Format("Subscription {0} unexpected StreamEventsSlice.Status: {0}.", (object) this.SubscriptionName, (object) slice.Status));
      }
      if (!done && slice.IsEndOfStream)
        Thread.Sleep(1);
      return done;
    }

    /// <summary>
    /// Try to process a single <see cref="T:EventStore.ClientAPI.ResolvedEvent" />.
    /// </summary>
    /// <param name="e">The <see cref="T:EventStore.ClientAPI.ResolvedEvent" /> to process.</param>
    protected override async Task TryProcessAsync(ResolvedEvent e)
    {
      bool flag = false;
      if (e.OriginalEventNumber > this._lastProcessedEventNumber)
      {
        try
        {
          await this.EventAppeared((EventStoreCatchUpSubscription) this, e).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
          this.DropSubscription(SubscriptionDropReason.EventHandlerException, ex);
          throw;
        }
        this._lastProcessedEventNumber = e.OriginalEventNumber;
        flag = true;
      }
      if (!this.Verbose)
        return;
      this.Log.Debug("Catch-up Subscription {0} to {1}: {2} event ({3}, {4}, {5} @ {6}).", (object) this.SubscriptionName, this.IsSubscribedToAll ? (object) "<all>" : (object) this.StreamId, flag ? (object) "processed" : (object) "skipping", (object) e.OriginalEvent.EventStreamId, (object) e.OriginalEvent.EventNumber, (object) e.OriginalEvent.EventType, (object) e.OriginalEventNumber);
    }
  }
}
