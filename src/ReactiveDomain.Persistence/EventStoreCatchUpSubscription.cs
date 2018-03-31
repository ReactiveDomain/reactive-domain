using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using ReactiveDomain.Logging;
using ReactiveDomain.Util;

namespace ReactiveDomain {
   public abstract class CatchUpSubscription
  {
    private readonly ConcurrentQueue<RecordedEvent> _liveQueue = new ConcurrentQueue<RecordedEvent>();
    private readonly ManualResetEventSlim _stopped = new ManualResetEventSlim(true);
    private static readonly RecordedEvent DropSubscriptionEvent;
    /// <summary>
    /// The <see cref="T:ReactiveDomain.ILogger" /> to use for the subscription.
    /// </summary>
    protected readonly ILogger Log;
    private readonly IStreamStoreConnection _connection;
    private readonly UserCredentials _userCredentials;
    /// <summary>
    /// The batch size to use during the read phase of the subscription.
    /// </summary>
    protected readonly int ReadBatchSize;
    /// <summary>
    /// The maximum number of events to buffer before the subscription drops.
    /// </summary>
    protected readonly int MaxPushQueueSize;
    /// <summary>
    /// Action invoked when a new event appears on the subscription.
    /// </summary>
    protected readonly Func<CatchUpSubscription, RecordedEvent, Task> EventAppeared;
    private readonly Action<CatchUpSubscription> _liveProcessingStarted;
    private readonly Action<CatchUpSubscription, SubscriptionDropReason, Exception> _subscriptionDropped;
    /// <summary>
    /// Whether or not to use verbose logging (useful during debugging).
    /// </summary>
    protected readonly bool Verbose;
    private StreamSubscription _subscription;
    private DropData _dropData;
    private volatile bool _allowProcessing;
    private int _isProcessing;
    /// <summary>stop has been called.</summary>
    protected volatile bool ShouldStop;
    private int _isDropped;

    /// <summary>
    /// Indicates whether the subscription is to all events or to
    /// a specific stream.
    /// </summary>
    public bool IsSubscribedToAll => StreamId == string.Empty;

      /// <summary>
    /// The name of the stream to which the subscription is subscribed
    /// (empty if subscribed to all).
    /// </summary>
    public string StreamId { get; }

    /// <summary>The name of subscription.</summary>
    public string SubscriptionName { get; }

    /// <summary>
    /// Read events until the given position or event number async.
    /// </summary>
    /// <param name="connection">The connection.</param>
    /// <param name="userCredentials">User credentials for the operation.</param>
    /// <param name="lastCommitPosition">The commit position to read until.</param>
    /// <param name="lastEventNumber">The event number to read until.</param>
    /// <returns>
    /// </returns>
    protected abstract Task ReadEventsTillAsync(IStreamStoreConnection connection, UserCredentials userCredentials, long? lastCommitPosition, long? lastEventNumber);

    /// <summary>
    /// Try to process a single <see cref="T:ReactiveDomain.ResolvedEvent" />.
    /// </summary>
    /// <param name="e">The <see cref="T:ReactiveDomain.ResolvedEvent" /> to process.</param>
    protected abstract Task TryProcessAsync(RecordedEvent e);

    /// <summary>Constructs state for EventStoreCatchUpSubscription.</summary>
    /// <param name="connection">The connection.</param>
    /// <param name="log">The <see cref="T:ReactiveDomain.ILogger" /> to use.</param>
    /// <param name="streamId">The stream name.</param>
    /// <param name="userCredentials">User credentials for the operations.</param>
    /// <param name="eventAppeared">Action invoked when events are received.</param>
    /// <param name="liveProcessingStarted">Action invoked when the read phase finishes.</param>
    /// <param name="subscriptionDropped">Action invoked if the subscription drops.</param>
    /// <param name="settings">Settings for this subscription.</param>
    protected CatchUpSubscription(
                IStreamStoreConnection connection, 
                ILogger log, 
                string streamId, 
                UserCredentials userCredentials, 
                Func<CatchUpSubscription, RecordedEvent, Task> eventAppeared, 
                Action<CatchUpSubscription> liveProcessingStarted, 
                Action<CatchUpSubscription, SubscriptionDropReason, Exception> subscriptionDropped, 
                CatchUpSubscriptionSettings settings)
    {
      Ensure.NotNull(connection, nameof (connection));
      Ensure.NotNull(log, nameof (log));
      Ensure.NotNull(eventAppeared, nameof (eventAppeared));
      _connection = connection;
      Log = log;
      StreamId = string.IsNullOrEmpty(streamId) ? string.Empty : streamId;
      _userCredentials = userCredentials;
      ReadBatchSize = settings.ReadBatchSize;
      MaxPushQueueSize = settings.MaxLiveQueueSize;
      EventAppeared = eventAppeared;
      _liveProcessingStarted = liveProcessingStarted;
      _subscriptionDropped = subscriptionDropped;
      Verbose = settings.VerboseLogging;
      SubscriptionName = settings.SubscriptionName ?? string.Empty;
    }

    internal Task StartAsync()
    {
      if (Verbose)
        Log.Debug("Catch-up Subscription {0} to {1}: starting...", SubscriptionName, IsSubscribedToAll ?  "<all>" :  StreamId);
      return RunSubscriptionAsync();
    }

    /// <summary>
    /// Attempts to stop the subscription blocking for completion of stop.
    /// </summary>
    /// <param name="timeout">The maximum amount of time which the current thread will block waiting for the subscription to stop before throwing a TimeoutException.</param>
    /// <exception cref="T:System.TimeoutException">Thrown if the subscription fails to stop within it's timeout period.</exception>
    public void Stop(TimeSpan timeout)
    {
      Stop();
      if (Verbose)
        Log.Debug("Waiting on subscription {0} to stop", (object) SubscriptionName);
      if (!_stopped.Wait(timeout))
        throw new TimeoutException(string.Format("Could not stop {0} in time.", GetType().Name));
    }

    /// <summary>
    /// Attempts to stop the subscription without blocking for completion of stop
    /// </summary>
    public void Stop()
    {
      if (Verbose)
        Log.Debug("Catch-up Subscription {0} to {1}: requesting stop...", SubscriptionName, IsSubscribedToAll ?  "<all>" :  StreamId);
      if (Verbose)
        Log.Debug("Catch-up Subscription {0} to {1}: unhooking from connection.Connected.", SubscriptionName, IsSubscribedToAll ?  "<all>" :  StreamId);
      _connection.Connected -= OnReconnect;
      ShouldStop = true;
      EnqueueSubscriptionDropNotification(SubscriptionDropReason.UserInitiated, null);
    }

    private void OnReconnect(object sender, ClientConnectionEventArgs clientConnectionEventArgs)
    {
      if (Verbose)
        Log.Debug("Catch-up Subscription {0} to {1}: recovering after reconnection.", SubscriptionName, IsSubscribedToAll ?  "<all>" :  StreamId);
      if (Verbose)
        Log.Debug("Catch-up Subscription {0} to {1}: unhooking from connection.Connected.", SubscriptionName, IsSubscribedToAll ?  "<all>" :  StreamId);
      _connection.Connected -= OnReconnect;
      RunSubscriptionAsync();
    }

    private Task RunSubscriptionAsync()
    {
      return LoadHistoricalEventsAsync();
    }

    private async Task LoadHistoricalEventsAsync()
    {
      if (Verbose)
        Log.Debug("Catch-up Subscription {0} to {1}: running...", SubscriptionName, IsSubscribedToAll ?  "<all>" :  StreamId);
      _stopped.Reset();
      _allowProcessing = false;
      if (!ShouldStop)
      {
        if (Verbose)
          Log.Debug("Catch-up Subscription {0} to {1}: pulling events...", SubscriptionName, IsSubscribedToAll ?  "<all>" :  StreamId);
        try
        {
          await ReadEventsTillAsync(_connection,  _userCredentials, new long?(), new long?()).ConfigureAwait(false);
          await SubscribeToStreamAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
          DropSubscription(SubscriptionDropReason.CatchUpError, ex);
          throw;
        }
      }
      else
        DropSubscription(SubscriptionDropReason.UserInitiated, null);
    }

    private async Task SubscribeToStreamAsync()
    {
      if (!ShouldStop)
      {
        if (Verbose)
          Log.Debug("Catch-up Subscription {0} to {1}: subscribing...", SubscriptionName, IsSubscribedToAll ?  "<all>" :  StreamId);
        StreamSubscription storeSubscription;
        if (StreamId == string.Empty)
          storeSubscription =  _connection.SubscribeToAll(EnqueuePushedEvent, ServerSubscriptionDropped, _userCredentials);
        else
          storeSubscription =  _connection.SubscribeToStream(StreamId, EnqueuePushedEvent, ServerSubscriptionDropped, _userCredentials);
        _subscription = storeSubscription;
        await ReadMissedHistoricEventsAsync().ConfigureAwait(false);
      }
      else
        DropSubscription(SubscriptionDropReason.UserInitiated, null);
    }

    private async Task ReadMissedHistoricEventsAsync()
    {
      if (!ShouldStop)
      {
        if (Verbose)
          Log.Debug("Catch-up Subscription {0} to {1}: pulling events (if left)...", SubscriptionName, IsSubscribedToAll ?  "<all>" :  StreamId);
        await ReadEventsTillAsync(_connection, _userCredentials, _subscription.LastCommitPosition, _subscription.LastEventNumber).ConfigureAwait(false);
        StartLiveProcessing();
      }
      else
        DropSubscription(SubscriptionDropReason.UserInitiated, null);
    }

    private void StartLiveProcessing()
    {
      if (ShouldStop)
      {
        DropSubscription(SubscriptionDropReason.UserInitiated, null);
      }
      else
      {
        if (Verbose)
          Log.Debug("Catch-up Subscription {0} to {1}: processing live events...", SubscriptionName, IsSubscribedToAll ?  "<all>" :  StreamId);
        if (_liveProcessingStarted != null)
          _liveProcessingStarted(this);
        if (Verbose)
          Log.Debug("Catch-up Subscription {0} to {1}: hooking to connection.Connected", SubscriptionName, IsSubscribedToAll ?  "<all>" :  StreamId);
        _connection.Connected += OnReconnect;
        _allowProcessing = true;
        EnsureProcessingPushQueue();
      }
    }

    private void EnqueuePushedEvent(StreamSubscription subscription, RecordedEvent e)
    {
      if (Verbose)
        Log.Debug($"Catch-up Subscription {SubscriptionName} to {(IsSubscribedToAll ? "<all>" : StreamId)}: event appeared ({e.EventStreamId}, {e.EventNumber}, {e.EventType}).");
      if (_liveQueue.Count >= MaxPushQueueSize)
      {
        EnqueueSubscriptionDropNotification(SubscriptionDropReason.ProcessingQueueOverflow, null);
        subscription.Unsubscribe();
      }
      _liveQueue.Enqueue(e);
      if (_allowProcessing)
        EnsureProcessingPushQueue();
    }

    private void ServerSubscriptionDropped(StreamSubscription subscription, SubscriptionDropReason reason, Exception exc)
    {
      EnqueueSubscriptionDropNotification(reason, exc);
    }

    private void EnqueueSubscriptionDropNotification(SubscriptionDropReason reason, Exception error)
    {
      if (Interlocked.CompareExchange(ref _dropData, new DropData(reason, error), null) != null)
        return;
      _liveQueue.Enqueue(DropSubscriptionEvent);
      if (!_allowProcessing)
        return;
      EnsureProcessingPushQueue();
    }

    private void EnsureProcessingPushQueue()
    {
      if (Interlocked.CompareExchange(ref _isProcessing, 1, 0) != 0)
        return;
      ThreadPool.QueueUserWorkItem(_ => ProcessLiveQueueAsync());
    }

    private async void ProcessLiveQueueAsync()
    {
      do
      {
          RecordedEvent e;
        while (_liveQueue.TryDequeue(out e))
        {
          if (e.Equals(DropSubscriptionEvent))
          {
            _dropData = _dropData ?? new DropData(SubscriptionDropReason.Unknown, new Exception("Drop reason not specified."));
            DropSubscription(_dropData.Reason, _dropData.Error);
            Interlocked.CompareExchange(ref _isProcessing, 0, 1);
            return;
          }
          try
          {
            await TryProcessAsync(e).ConfigureAwait(false);
          }
          catch (Exception ex)
          {
            Log.Debug("Catch-up Subscription {0} to {1} Exception occurred in subscription {1}", SubscriptionName, IsSubscribedToAll ? "<all>" : StreamId, ex);
            DropSubscription(SubscriptionDropReason.EventHandlerException, ex);
            return;
          }
        }
        Interlocked.CompareExchange(ref _isProcessing, 0, 1);
        //e = new RecordedEvent();
      }
      while (_liveQueue.Count > 0 && Interlocked.CompareExchange(ref _isProcessing, 1, 0) == 0);
    }

    internal void DropSubscription(SubscriptionDropReason reason, Exception error)
    {
      if (Interlocked.CompareExchange(ref _isDropped, 1, 0) != 0)
        return;
      if (Verbose)
        Log.Debug("Catch-up Subscription {0} to {1}: dropping subscription, reason: {2} {3}.", SubscriptionName, IsSubscribedToAll ? "<all>" : StreamId, reason, error == null ? string.Empty : error.ToString());
      StreamSubscription subscription = _subscription;
      if (subscription != null)
        subscription.Unsubscribe();
      Action<CatchUpSubscription, SubscriptionDropReason, Exception> subscriptionDropped = _subscriptionDropped;
      if (subscriptionDropped != null)
        subscriptionDropped(this, reason, error);
      _stopped.Set();
    }

    private class DropData
    {
      public readonly SubscriptionDropReason Reason;
      public readonly Exception Error;

      public DropData(SubscriptionDropReason reason, Exception error)
      {
        Reason = reason;
        Error = error;
      }
    }
  }
}