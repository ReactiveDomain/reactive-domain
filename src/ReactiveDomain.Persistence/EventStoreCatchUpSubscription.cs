using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using ReactiveDomain.Util;
using ILogger = ReactiveDomain.Logging.ILogger;

namespace ReactiveDomain {
   public abstract class EventStoreCatchUpSubscription
  {
    private readonly ConcurrentQueue<ResolvedEvent> _liveQueue = new ConcurrentQueue<ResolvedEvent>();
    private readonly ManualResetEventSlim _stopped = new ManualResetEventSlim(true);
    private static readonly ResolvedEvent DropSubscriptionEvent;
    /// <summary>
    /// The <see cref="T:EventStore.ClientAPI.ILogger" /> to use for the subscription.
    /// </summary>
    protected readonly ILogger Log;
    private readonly IStreamStoreConnection _connection;
    private readonly bool _resolveLinkTos;
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
    protected readonly Func<EventStoreCatchUpSubscription, ResolvedEvent, Task> EventAppeared;
    private readonly Action<EventStoreCatchUpSubscription> _liveProcessingStarted;
    private readonly Action<EventStoreCatchUpSubscription, SubscriptionDropReason, Exception> _subscriptionDropped;
    /// <summary>
    /// Whether or not to use verbose logging (useful during debugging).
    /// </summary>
    protected readonly bool Verbose;
    private EventStoreSubscription _subscription;
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
    public bool IsSubscribedToAll
    {
      get
      {
        return StreamId == string.Empty;
      }
    }

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
    /// <param name="resolveLinkTos">Whether to resolve Link events.</param>
    /// <param name="userCredentials">User credentials for the operation.</param>
    /// <param name="lastCommitPosition">The commit position to read until.</param>
    /// <param name="lastEventNumber">The event number to read until.</param>
    /// <returns>
    /// </returns>
    protected abstract Task ReadEventsTillAsync(IStreamStoreConnection connection, bool resolveLinkTos, UserCredentials userCredentials, long? lastCommitPosition, long? lastEventNumber);

    /// <summary>
    /// Try to process a single <see cref="T:EventStore.ClientAPI.ResolvedEvent" />.
    /// </summary>
    /// <param name="e">The <see cref="T:EventStore.ClientAPI.ResolvedEvent" /> to process.</param>
    protected abstract Task TryProcessAsync(ResolvedEvent e);

    /// <summary>Constructs state for EventStoreCatchUpSubscription.</summary>
    /// <param name="connection">The connection.</param>
    /// <param name="log">The <see cref="T:EventStore.ClientAPI.ILogger" /> to use.</param>
    /// <param name="streamId">The stream name.</param>
    /// <param name="userCredentials">User credentials for the operations.</param>
    /// <param name="eventAppeared">Action invoked when events are received.</param>
    /// <param name="liveProcessingStarted">Action invoked when the read phase finishes.</param>
    /// <param name="subscriptionDropped">Action invoked if the subscription drops.</param>
    /// <param name="settings">Settings for this subscription.</param>
    protected EventStoreCatchUpSubscription(IStreamStoreConnection connection, ILogger log, string streamId, UserCredentials userCredentials, Func<EventStoreCatchUpSubscription, ResolvedEvent, Task> eventAppeared, Action<EventStoreCatchUpSubscription> liveProcessingStarted, Action<EventStoreCatchUpSubscription, SubscriptionDropReason, Exception> subscriptionDropped, CatchUpSubscriptionSettings settings)
    {
      Ensure.NotNull<IStreamStoreConnection>(connection, nameof (connection));
      Ensure.NotNull<ILogger>(log, nameof (log));
      Ensure.NotNull<Func<EventStoreCatchUpSubscription, ResolvedEvent, Task>>(eventAppeared, nameof (eventAppeared));
      _connection = connection;
      Log = log;
      StreamId = string.IsNullOrEmpty(streamId) ? string.Empty : streamId;
      _resolveLinkTos = settings.ResolveLinkTos;
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
        Log.Debug("Catch-up Subscription {0} to {1}: starting...", new object[2]
        {
          (object) SubscriptionName,
          IsSubscribedToAll ? (object) "<all>" : (object) StreamId
        });
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
        throw new TimeoutException(string.Format("Could not stop {0} in time.", (object) GetType().Name));
    }

    /// <summary>
    /// Attempts to stop the subscription without blocking for completion of stop
    /// </summary>
    public void Stop()
    {
      if (Verbose)
        Log.Debug("Catch-up Subscription {0} to {1}: requesting stop...", new object[2]
        {
          (object) SubscriptionName,
          IsSubscribedToAll ? (object) "<all>" : (object) StreamId
        });
      if (Verbose)
        Log.Debug("Catch-up Subscription {0} to {1}: unhooking from connection.Connected.", new object[2]
        {
          (object) SubscriptionName,
          IsSubscribedToAll ? (object) "<all>" : (object) StreamId
        });
      _connection.Connected -= OnReconnect;
      ShouldStop = true;
      EnqueueSubscriptionDropNotification(SubscriptionDropReason.UserInitiated, (Exception) null);
    }

    private void OnReconnect(object sender, ClientConnectionEventArgs clientConnectionEventArgs)
    {
      if (Verbose)
        Log.Debug("Catch-up Subscription {0} to {1}: recovering after reconnection.", new object[2]
        {
          (object) SubscriptionName,
          IsSubscribedToAll ? (object) "<all>" : (object) StreamId
        });
      if (Verbose)
        Log.Debug("Catch-up Subscription {0} to {1}: unhooking from connection.Connected.", new object[2]
        {
          (object) SubscriptionName,
          IsSubscribedToAll ? (object) "<all>" : (object) StreamId
        });
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
        Log.Debug("Catch-up Subscription {0} to {1}: running...", new object[2]
        {
          (object) SubscriptionName,
          IsSubscribedToAll ? (object) "<all>" : (object) StreamId
        });
      _stopped.Reset();
      _allowProcessing = false;
      if (!ShouldStop)
      {
        if (Verbose)
          Log.Debug("Catch-up Subscription {0} to {1}: pulling events...", new object[2]
          {
            (object) SubscriptionName,
            IsSubscribedToAll ? (object) "<all>" : (object) StreamId
          });
        try
        {
          await ReadEventsTillAsync(_connection, _resolveLinkTos, _userCredentials, new long?(), new long?()).ConfigureAwait(false);
          await SubscribeToStreamAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
          DropSubscription(SubscriptionDropReason.CatchUpError, ex);
          throw;
        }
      }
      else
        DropSubscription(SubscriptionDropReason.UserInitiated, (Exception) null);
    }

    private async Task SubscribeToStreamAsync()
    {
      if (!ShouldStop)
      {
        if (Verbose)
          Log.Debug("Catch-up Subscription {0} to {1}: subscribing...", new object[2]
          {
            (object) SubscriptionName,
            IsSubscribedToAll ? (object) "<all>" : (object) StreamId
          });
        EventStoreSubscription storeSubscription;
        if (StreamId == string.Empty)
          storeSubscription = await _connection.SubscribeToAllAsync(_resolveLinkTos, EnqueuePushedEvent, ServerSubscriptionDropped, _userCredentials).ConfigureAwait(false);
        else
          storeSubscription = await _connection.SubscribeToStreamAsync(StreamId, _resolveLinkTos, EnqueuePushedEvent, ServerSubscriptionDropped, _userCredentials).ConfigureAwait(false);
        _subscription = storeSubscription;
        await ReadMissedHistoricEventsAsync().ConfigureAwait(false);
      }
      else
        DropSubscription(SubscriptionDropReason.UserInitiated, (Exception) null);
    }

    private async Task ReadMissedHistoricEventsAsync()
    {
      if (!ShouldStop)
      {
        if (Verbose)
          Log.Debug("Catch-up Subscription {0} to {1}: pulling events (if left)...", new object[2]
          {
            (object) SubscriptionName,
            IsSubscribedToAll ? (object) "<all>" : (object) StreamId
          });
        await ReadEventsTillAsync(_connection, _resolveLinkTos, _userCredentials, new long?(_subscription.LastCommitPosition), _subscription.LastEventNumber).ConfigureAwait(false);
        StartLiveProcessing();
      }
      else
        DropSubscription(SubscriptionDropReason.UserInitiated, (Exception) null);
    }

    private void StartLiveProcessing()
    {
      if (ShouldStop)
      {
        DropSubscription(SubscriptionDropReason.UserInitiated, (Exception) null);
      }
      else
      {
        if (Verbose)
          Log.Debug("Catch-up Subscription {0} to {1}: processing live events...", new object[2]
          {
            (object) SubscriptionName,
            IsSubscribedToAll ? (object) "<all>" : (object) StreamId
          });
        if (_liveProcessingStarted != null)
          _liveProcessingStarted(this);
        if (Verbose)
          Log.Debug("Catch-up Subscription {0} to {1}: hooking to connection.Connected", new object[2]
          {
            (object) SubscriptionName,
            IsSubscribedToAll ? (object) "<all>" : (object) StreamId
          });
        _connection.Connected += OnReconnect;
        _allowProcessing = true;
        EnsureProcessingPushQueue();
      }
    }

    private void EnqueuePushedEvent(EventStoreSubscription subscription, ResolvedEvent e)
    {
      if (Verbose)
        Log.Debug("Catch-up Subscription {0} to {1}: event appeared ({2}, {3}, {4} @ {5}).", (object) SubscriptionName, IsSubscribedToAll ? (object) "<all>" : (object) StreamId, (object) e.OriginalStreamId, (object) e.OriginalEventNumber, (object) e.OriginalEvent.EventType, (object) e.OriginalPosition);
      if (_liveQueue.Count >= MaxPushQueueSize)
      {
        EnqueueSubscriptionDropNotification(SubscriptionDropReason.ProcessingQueueOverflow, (Exception) null);
        subscription.Unsubscribe();
      }
      _liveQueue.Enqueue(e);
      if (_allowProcessing)
        EnsureProcessingPushQueue();
    }

    private void ServerSubscriptionDropped(EventStoreSubscription subscription, SubscriptionDropReason reason, Exception exc)
    {
      EnqueueSubscriptionDropNotification(reason, exc);
    }

    private void EnqueueSubscriptionDropNotification(SubscriptionDropReason reason, Exception error)
    {
      if (Interlocked.CompareExchange<DropData>(ref _dropData, new DropData(reason, error), (DropData) null) != null)
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
      ThreadPool.QueueUserWorkItem((WaitCallback) (_ => ProcessLiveQueueAsync()));
    }

    private async void ProcessLiveQueueAsync()
    {
      do
      {
        ResolvedEvent e;
        while (_liveQueue.TryDequeue(out e))
        {
          if (e.Equals((object) DropSubscriptionEvent))
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
            Log.Debug("Catch-up Subscription {0} to {1} Exception occurred in subscription {1}", (object) SubscriptionName, IsSubscribedToAll ? (object) "<all>" : (object) StreamId, (object) ex);
            DropSubscription(SubscriptionDropReason.EventHandlerException, ex);
            return;
          }
        }
        Interlocked.CompareExchange(ref _isProcessing, 0, 1);
        e = new ResolvedEvent();
      }
      while (_liveQueue.Count > 0 && Interlocked.CompareExchange(ref _isProcessing, 1, 0) == 0);
    }

    internal void DropSubscription(SubscriptionDropReason reason, Exception error)
    {
      if (Interlocked.CompareExchange(ref _isDropped, 1, 0) != 0)
        return;
      if (Verbose)
        Log.Debug("Catch-up Subscription {0} to {1}: dropping subscription, reason: {2} {3}.", (object) SubscriptionName, IsSubscribedToAll ? (object) "<all>" : (object) StreamId, (object) reason, error == null ? (object) string.Empty : (object) error.ToString());
      EventStoreSubscription subscription = _subscription;
      if (subscription != null)
        subscription.Unsubscribe();
      Action<EventStoreCatchUpSubscription, SubscriptionDropReason, Exception> subscriptionDropped = _subscriptionDropped;
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