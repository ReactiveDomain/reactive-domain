using System.Diagnostics;
using System.Reactive;
using Grpc.Core.Interceptors;
using KurrentDB.Client;
using ReactiveDomain.Grpc;
using ReactiveDomain.Logging;
using EsdbEventData = KurrentDB.Client.EventData;
using EsdbPosition = KurrentDB.Client.Position;
using EsdbStreamPosition = KurrentDB.Client.StreamPosition;
using EventDataRD = ReactiveDomain.EventData;
using PositionRD = ReactiveDomain.Position;
using RecordedEventRD = ReactiveDomain.RecordedEvent;
using SubscriptionDropReasonRD = ReactiveDomain.SubscriptionDropReason;
using UserCredentialsRD = ReactiveDomain.UserCredentials;
using WrongExpectedVersionExceptionRD = ReactiveDomain.WrongExpectedVersionException;

namespace ReactiveDomain.EventStore;

/// <summary>
/// <see cref="IStreamStoreConnectionGrpc"/> over stock <see cref="KurrentDBClient"/> (KurrentDB / ESDB
/// gRPC wire). V1 <c>SubscribeToAll</c> / <c>SubscribeToAllFrom</c> keep the classic resolve-links
/// default — use <see cref="IStreamStoreConnectionGrpc.SubscribeToAllFiltered"/> with
/// <see cref="StreamStoreEventFilter.ExcludeSystemEvents"/> for the domain-only <c>$all</c> view.
/// </summary>
/// <remarks>
/// Authentication is connection-scoped via gRPC client interceptors, analogous to optional
/// connection-level <see cref="UserCredentials"/> on <see cref="EventStoreConnectionWrapper"/>.
/// Pass optional <see cref="UserCredentials"/> on the primary constructor for basic auth, or use the
/// <see cref="BearerToken"/> constructor for bearer auth — do not mix schemes. Per-call
/// <see cref="UserCredentials"/> on individual methods are not supported and throw
/// <see cref="NotSupportedException"/>: the .NET gRPC client drops per-call credentials on insecure
/// (h2c) channels, so connection-wide headers are the reliable mechanism (unlike the TCP wrapper).
/// </remarks>
public sealed class GrpcConnectionWrapper : IStreamStoreConnectionGrpc {
	private readonly KurrentDBClient _client;
	private readonly ILogger _logger;
	private readonly HashSet<IDisposable> _subscriptions = new();
	private readonly object _subscriptionsLock = new();
	private int _disposed;

	/// <summary>
	/// Creates a connection, optionally with connection-scoped basic auth (same credential type as
	/// <see cref="EventStoreConnectionWrapper"/>).
	/// </summary>
	/// <param name="connectionName">Name used for log disambiguation.</param>
	/// <param name="connectionString">KurrentDB / ESDB URI (e.g. <c>esdb://host:2113?tls=false</c>).</param>
	/// <param name="credentials">
	/// Optional username/password stamped on every call via a basic-auth interceptor. Omit or pass
	/// null for no authentication.
	/// </param>
	/// <param name="partition">
	/// Optional partition header value stamped on every call. Pass as a named argument when also
	/// omitting credentials.
	/// </param>
	/// <param name="createHttpMessageHandler">
	/// Optional handler factory (e.g. ASP.NET TestServer <c>CreateHandler</c>) so the client can talk
	/// to an in-process host with no socket.
	/// </param>
	/// <param name="logger">
	/// Optional logger; defaults to <see cref="LogManager.GetLogger(string)"/> for this type.
	/// </param>
	public GrpcConnectionWrapper(
		string connectionName,
		Uri connectionString,
		UserCredentialsRD? credentials = null,
		string? partition = null,
		Func<HttpMessageHandler>? createHttpMessageHandler = null,
		ILogger? logger = null) {
		ConnectionName = connectionName;
		_logger = logger ?? LogManager.GetLogger(nameof(GrpcConnectionWrapper));
		_client = CreateClient(connectionString, partition, createHttpMessageHandler, credentials);
	}

	/// <summary>
	/// Creates a connection that stamps a bearer token on every gRPC call.
	/// </summary>
	/// <param name="connectionName">Name used for log disambiguation.</param>
	/// <param name="connectionString">KurrentDB / ESDB URI (e.g. <c>esdb://host:2113?tls=false</c>).</param>
	/// <param name="bearerToken">Bearer token for the <c>authorization</c> header.</param>
	/// <param name="partition">Optional partition header value stamped on every call.</param>
	/// <param name="createHttpMessageHandler">
	/// Optional handler factory (e.g. ASP.NET TestServer <c>CreateHandler</c>) so the client can talk
	/// to an in-process host with no socket.
	/// </param>
	/// <param name="logger">
	/// Optional logger; defaults to <see cref="LogManager.GetLogger(string)"/> for this type.
	/// </param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="bearerToken"/> is null.</exception>
	public GrpcConnectionWrapper(
		string connectionName,
		Uri connectionString,
		BearerToken bearerToken,
		string? partition = null,
		Func<HttpMessageHandler>? createHttpMessageHandler = null,
		ILogger? logger = null) {
		ArgumentNullException.ThrowIfNull(bearerToken);
		ConnectionName = connectionName;
		_logger = logger ?? LogManager.GetLogger(nameof(GrpcConnectionWrapper));
		_client = CreateClient(connectionString, partition, createHttpMessageHandler, bearerToken);
	}

	private static KurrentDBClient CreateClient(
		Uri connectionString,
		string? partition,
		Func<HttpMessageHandler>? createHttpMessageHandler,
		UserCredentialsRD? credentials) =>
		CreateClientCore(connectionString, partition, createHttpMessageHandler,
			credentials is null
				? null
				: [new BasicAuthInterceptor(credentials.Username, credentials.Password)]);

	private static KurrentDBClient CreateClient(
		Uri connectionString,
		string? partition,
		Func<HttpMessageHandler>? createHttpMessageHandler,
		BearerToken bearerToken) =>
		CreateClientCore(connectionString, partition, createHttpMessageHandler,
			[new BearerTokenInterceptor(bearerToken.Value)]);

	private static KurrentDBClient CreateClientCore(
		Uri connectionString,
		string? partition,
		Func<HttpMessageHandler>? createHttpMessageHandler,
		List<Interceptor>? interceptors) {
		var settings = KurrentDBClientSettings.Create(connectionString.ToString());
		settings.DefaultDeadline = TimeSpan.FromMinutes(2);
		if (createHttpMessageHandler is not null) {
			settings.CreateHttpMessageHandler = createHttpMessageHandler;
		}

		interceptors ??= [];
		if (partition is not null) {
			interceptors.Add(new PartitionInterceptor(partition));
		}
		if (interceptors.Count > 0) {
			settings.Interceptors = interceptors;
		}
		return new KurrentDBClient(settings);
	}

	/// <summary>
	/// Per-call credentials are not supported on the gRPC wrapper. Pass connection-level
	/// <see cref="UserCredentials"/> or a <see cref="BearerToken"/> to the constructor instead.
	/// Unlike <see cref="EventStoreConnectionWrapper"/>, method-level credentials cannot override
	/// connection auth (gRPC <c>CallCredentials</c> are dropped on insecure channels).
	/// </summary>
	/// <exception cref="NotSupportedException">Always thrown when <paramref name="credentials"/> is non-null.</exception>
	internal static void RejectPerCallCredentials(UserCredentialsRD? credentials) {
		if (credentials is not null) {
			throw new NotSupportedException(
				"Per-call UserCredentials are not supported on GrpcConnectionWrapper. " +
				"Pass UserCredentials or BearerToken to the constructor so auth is stamped via " +
				"connection-wide gRPC interceptors (required on h2c / tls=false channels).");
		}
	}

	/// <inheritdoc cref="IStreamStoreConnection"/>
	public string ConnectionName { get; }

	private IDisposable TrackSubscription(CancellationTokenSource cts) {
		var subscription = new SubscriptionDisposable(cts);
		lock (_subscriptionsLock) {
			_subscriptions.Add(subscription);
		}
		return subscription;
	}

	/// <inheritdoc cref="IStreamStoreConnection"/>
	public void Dispose() => Close();

	/// <inheritdoc cref="IStreamStoreConnection"/>
	/// <remarks>
	/// No-op that satisfies <see cref="IStreamStoreConnection"/>. The underlying
	/// <see cref="KurrentDBClient"/> is created during construction and is ready for use; the client
	/// may connect lazily on the first RPC.
	/// </remarks>
	public void Connect() { }

	/// <inheritdoc cref="IStreamStoreConnection"/>
	/// <remarks>
	/// Shuts everything down: cancels/disposes all tracked subscription pumps, then disposes the
	/// underlying <see cref="KurrentDBClient"/>. Idempotent. In-flight callbacks may still run
	/// briefly after return; that is accepted — the goal is full teardown, not a quiet barrier.
	/// </remarks>
	public void Close() {
		if (Interlocked.Exchange(ref _disposed, 1) != 0) {
			return;
		}

		IDisposable[] outstanding;
		lock (_subscriptionsLock) {
			outstanding = [.. _subscriptions];
			_subscriptions.Clear();
		}
		foreach (var subscription in outstanding) {
			subscription.Dispose();
		}
		_client.Dispose();
	}

	/// <inheritdoc cref="IStreamStoreConnection"/>
	public WriteResult AppendToStream(string stream, long expectedVersion, UserCredentialsRD? credentials = null,
		params EventDataRD[] events) {
		RejectPerCallCredentials(credentials);
		ArgumentOutOfRangeException.ThrowIfLessThan(expectedVersion, -3);
		IWriteResult result;
		try {
			result = _client.AppendToStreamAsync(stream, ToStreamState(expectedVersion), events.Select(ToEventData))
				.GetAwaiter().GetResult();
		} catch (KurrentDB.Client.WrongExpectedVersionException ex) {
			throw new WrongExpectedVersionExceptionRD(stream, (int)ex.ActualStreamState.ToInt64(),
				(int)expectedVersion, ex);
		}

		return result switch {
			SuccessResult success => new WriteResult(success.NextExpectedVersion),
			WrongExpectedVersionResult wev => throw new WrongExpectedVersionExceptionRD(stream,
				(int)wev.ActualStreamState.ToInt64(), (int)expectedVersion),
			_ => throw new InvalidOperationException($"Unexpected result type: {result.GetType().Name}")
		};
	}

	/// <inheritdoc cref="IStreamStoreConnection"/>
	public StreamEventsSlice ReadStreamForward(string stream, long start, long count,
		UserCredentialsRD? credentials = null) {
		RejectPerCallCredentials(credentials);
		ArgumentOutOfRangeException.ThrowIfGreaterThan(count, int.MaxValue);
		ArgumentOutOfRangeException.ThrowIfNegative(start);

		var events = new List<RecordedEventRD>();
		var lastEventNumber = -1L;
		var notFound = false;
		foreach (var message in _client.ReadStreamAsync(Direction.Forwards, stream, new EsdbStreamPosition((ulong)start),
						 count, resolveLinkTos: true).Messages
					 .ToBlockingEnumerable()) {
			switch (message) {
				case StreamMessage.NotFound:
					notFound = true;
					break;
				case StreamMessage.LastStreamPosition(var position):
					lastEventNumber = position.ToInt64();
					break;
				case StreamMessage.Event(var resolvedEvent):
					var recorded = ToRecordedEvent(resolvedEvent);
					if (recorded is not null) {
						events.Add(recorded);
					}
					break;
			}
		}

		if (notFound) {
			return new StreamNotFoundSlice(stream);
		}

		if (events.Count == 0 && lastEventNumber < 0) {
			var last = TryGetLastEventNumber(stream);
			if (last is null) {
				return new StreamNotFoundSlice(stream);
			}

			lastEventNumber = last.Value;
		} else if (lastEventNumber < 0) {
			lastEventNumber = events[^1].EventNumber;
		}

		var nextEventNumber = events.Count > 0
			? events[^1].EventNumber + 1
			: Math.Min(start, lastEventNumber + 1);
		var isEndOfStream = (events.Count > 0 ? events[^1].EventNumber : start - 1) >= lastEventNumber;

		return new StreamEventsSlice(stream, start, ReadDirection.Forward, events.ToArray(), nextEventNumber,
			lastEventNumber, isEndOfStream);
	}

	/// <inheritdoc cref="IStreamStoreConnection"/>
	public StreamEventsSlice ReadStreamBackward(string stream, long start, long count,
		UserCredentialsRD? credentials = null) {
		RejectPerCallCredentials(credentials);
		ArgumentOutOfRangeException.ThrowIfGreaterThan(count, int.MaxValue);
		ArgumentOutOfRangeException.ThrowIfLessThan(start, -1);
		var from = start < 0 ? EsdbStreamPosition.End : new EsdbStreamPosition((ulong)start);

		var events = new List<RecordedEventRD>();
		var lastEventNumber = -1L;
		var notFound = false;
		foreach (var message in _client.ReadStreamAsync(Direction.Backwards, stream, from,
						 count, resolveLinkTos: true).Messages
					 .ToBlockingEnumerable()) {
			switch (message) {
				case StreamMessage.NotFound:
					notFound = true;
					break;
				case StreamMessage.LastStreamPosition(var position):
					lastEventNumber = position.ToInt64();
					break;
				case StreamMessage.Event(var resolvedEvent):
					var recorded = ToRecordedEvent(resolvedEvent);
					if (recorded is not null) {
						events.Add(recorded);
					}
					break;
			}
		}

		if (notFound) {
			return new StreamNotFoundSlice(stream);
		}

		if (events.Count == 0 && lastEventNumber < 0) {
			var last = TryGetLastEventNumber(stream);
			if (last is null) {
				return new StreamNotFoundSlice(stream);
			}

			lastEventNumber = last.Value;
		} else if (lastEventNumber < 0) {
			lastEventNumber = events.Count > 0 ? events[0].EventNumber : -1;
		}

		var nextEventNumber = events.Count > 0 ? events[^1].EventNumber - 1 : start - count;
		var isEndOfStream = events.Count > 0 && events[^1].EventNumber == 0;

		return new StreamEventsSlice(stream, start, ReadDirection.Backward, events.ToArray(), nextEventNumber,
			lastEventNumber, isEndOfStream);
	}

	private long? TryGetLastEventNumber(string stream) {
		foreach (var message in _client.ReadStreamAsync(Direction.Backwards, stream, EsdbStreamPosition.End, 1,
						 resolveLinkTos: false).Messages.ToBlockingEnumerable()) {
			switch (message) {
				case StreamMessage.NotFound:
					return null;
				case StreamMessage.Event(var resolvedEvent):
					// KurrentDB annotates Event as non-null, but it can be null for deleted/scavenged links.
					EventRecord? evt = resolvedEvent.Event;
					return evt is null ? null : evt.EventNumber.ToInt64();
			}
		}

		return null;
	}

	/// <inheritdoc cref="IStreamStoreConnection"/>
	public IDisposable SubscribeToStream(string stream, Action<RecordedEventRD> eventAppeared,
		Action<SubscriptionDropReasonRD, Exception?>? subscriptionDropped = null,
		UserCredentialsRD? credentials = null) {
		RejectPerCallCredentials(credentials);
		var cts = new CancellationTokenSource();
		var established = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		_ = Task.Run(async () => {
			try {
				await foreach (var message in _client.SubscribeToStream(stream, FromStream.End, resolveLinkTos: true,
								   cancellationToken: cts.Token)
								   .Messages.WithCancellation(cts.Token)) {
					established.TrySetResult();
					if (message is not StreamMessage.Event(var resolvedEvent)) {
						continue;
					}

					try {
						var recorded = ToRecordedEvent(resolvedEvent);
						if (recorded is not null) {
							eventAppeared(recorded);
						}
					} catch (Exception ex) {
						_logger.TraceException(ex, "An error occurred in the eventAppeared callback");
						subscriptionDropped?.Invoke(SubscriptionDropReasonRD.SubscribingError, ex);
						// return (not break): stop the pump after a handler throw — same contract as *From.
						return;
					}
				}
			} catch (OperationCanceledException ex) {
				established.TrySetResult();
				subscriptionDropped?.Invoke(SubscriptionDropReasonRD.UserInitiated, ex);
			} catch (Exception ex) {
				established.TrySetException(ex);
				_logger.TraceException(ex, "An error occurred in subscription callback");
				subscriptionDropped?.Invoke(SubscriptionDropReasonRD.ServerError, ex);
			}
		}, cts.Token);
		EnsureEstablished(established.Task, cts);
		return TrackSubscription(cts);
	}

	/// <inheritdoc cref="IStreamStoreConnection"/>
	public IDisposable SubscribeToStreamFrom(string stream, long? lastCheckpoint, CatchUpSubscriptionSettings? settings,
		Action<RecordedEventRD> eventAppeared, Action<Unit>? liveProcessingStarted = null,
		Action<SubscriptionDropReasonRD, Exception?>? subscriptionDropped = null,
		UserCredentialsRD? credentials = null) {
		RejectPerCallCredentials(credentials);
		var cts = new CancellationTokenSource();
		var established = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		_ = Task.Run(async () => {
			try {
				await foreach (var message in _client.SubscribeToStream(stream,
									   lastCheckpoint.HasValue
										   ? FromStream.After(new EsdbStreamPosition((ulong)lastCheckpoint))
										   : FromStream.Start,
									   resolveLinkTos: true,
									   cancellationToken: cts.Token)
								   .Messages.WithCancellation(cts.Token)) {
					established.TrySetResult();
					switch (message) {
						case StreamMessage.CaughtUp:
							liveProcessingStarted?.Invoke(Unit.Default);
							continue;
						case StreamMessage.Event(var resolvedEvent):
							try {
								var recorded = ToRecordedEvent(resolvedEvent);
								if (recorded is not null) {
									eventAppeared(recorded);
								}
								continue;
							} catch (Exception ex) {
								_logger.TraceException(ex, "An error occurred in the eventAppeared callback");
								subscriptionDropped?.Invoke(SubscriptionDropReasonRD.SubscribingError, ex);
								return;
							}
					}
				}
			} catch (OperationCanceledException ex) {
				established.TrySetResult();
				subscriptionDropped?.Invoke(SubscriptionDropReasonRD.UserInitiated, ex);
			} catch (Exception ex) {
				established.TrySetException(ex);
				_logger.TraceException(ex, "An error occurred in subscription callback");
				subscriptionDropped?.Invoke(SubscriptionDropReasonRD.ServerError, ex);
			}
		}, cts.Token);
		EnsureEstablished(established.Task, cts);
		return TrackSubscription(cts);
	}

	/// <inheritdoc cref="IStreamStoreConnection"/>
	public IDisposable SubscribeToAll(Action<RecordedEventRD> eventAppeared,
		Action<SubscriptionDropReasonRD, Exception?>? subscriptionDropped = null, UserCredentialsRD? credentials = null,
		bool resolveLinkTos = true) {
		RejectPerCallCredentials(credentials);
		var cts = new CancellationTokenSource();
		var established = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		_ = Task.Run(async () => {
			try {
				await foreach (var message in _client
								   .SubscribeToAll(FromAll.End, resolveLinkTos, cancellationToken: cts.Token)
								   .Messages.WithCancellation(cts.Token)) {
					established.TrySetResult();
					if (message is not StreamMessage.Event(var resolvedEvent))
						continue;
					try {
						var recorded = ToRecordedEvent(resolvedEvent);
						if (recorded is not null) {
							eventAppeared(recorded);
						}
					} catch (Exception ex) {
						_logger.TraceException(ex, "An error occurred in the eventAppeared callback");
						subscriptionDropped?.Invoke(SubscriptionDropReasonRD.SubscribingError, ex);
						// return (not break): stop the pump after a handler throw — same contract as *From.
						return;
					}
				}
			} catch (OperationCanceledException ex) {
				established.TrySetResult();
				subscriptionDropped?.Invoke(SubscriptionDropReasonRD.UserInitiated, ex);
			} catch (Exception ex) {
				established.TrySetException(ex);
				_logger.TraceException(ex, "An error occurred in subscription callback");
				subscriptionDropped?.Invoke(SubscriptionDropReasonRD.ServerError, ex);
			}
		}, cts.Token);
		EnsureEstablished(established.Task, cts);
		return TrackSubscription(cts);
	}

	/// <inheritdoc cref="IStreamStoreConnection"/>
	public IDisposable SubscribeToAllFrom(PositionRD from, Action<RecordedEventRD> eventAppeared,
		CatchUpSubscriptionSettings? settings = null,
		Action? liveProcessingStarted = null, Action<SubscriptionDropReasonRD, Exception?>? subscriptionDropped = null,
		UserCredentialsRD? credentials = null,
		bool resolveLinkTos = true) {
		RejectPerCallCredentials(credentials);
		var cts = new CancellationTokenSource();
		var established = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		_ = Task.Run(async () => {
			try {
				await foreach (var message in _client
								   .SubscribeToAll(FromAll.After(ToEsdbPosition(from)), resolveLinkTos,
									   cancellationToken: cts.Token)
								   .Messages.WithCancellation(cts.Token)) {
					established.TrySetResult();
					switch (message) {
						case StreamMessage.CaughtUp:
							liveProcessingStarted?.Invoke();
							continue;
						case StreamMessage.Event(var resolvedEvent):
							try {
								var recorded = ToRecordedEvent(resolvedEvent);
								if (recorded is not null) {
									eventAppeared(recorded);
								}
								continue;
							} catch (Exception ex) {
								_logger.TraceException(ex, "An error occurred in the eventAppeared callback");
								subscriptionDropped?.Invoke(SubscriptionDropReasonRD.SubscribingError, ex);
								return;
							}
					}
				}
			} catch (OperationCanceledException ex) {
				established.TrySetResult();
				subscriptionDropped?.Invoke(SubscriptionDropReasonRD.UserInitiated, ex);
			} catch (Exception ex) {
				established.TrySetException(ex);
				_logger.TraceException(ex, "An error occurred in subscription callback");
				subscriptionDropped?.Invoke(SubscriptionDropReasonRD.ServerError, ex);
			}
		}, cts.Token);
		EnsureEstablished(established.Task, cts);
		return TrackSubscription(cts);
	}

	/// <inheritdoc cref="IStreamStoreConnectionGrpc"/>
	public IDisposable SubscribeToAllFiltered(PositionRD from, Action<RecordedEventRD> eventAppeared,
		StreamStoreEventFilter? filter, Action? liveProcessingStarted = null,
		Action<PositionRD>? checkpointReached = null,
		Action<SubscriptionDropReasonRD, Exception?>? subscriptionDropped = null, bool resolveLinkTos = false) {
		var cts = new CancellationTokenSource();
		var established = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var filterOptions = filter is null ? null : new SubscriptionFilterOptions(ToClientFilter(filter));
		_ = Task.Run(async () => {
			try {
				await foreach (var message in _client
								   .SubscribeToAll(FromAll.After(ToEsdbPosition(from)), resolveLinkTos,
									   filterOptions, cancellationToken: cts.Token)
								   .Messages.WithCancellation(cts.Token)) {
					established.TrySetResult();
					switch (message) {
						case StreamMessage.CaughtUp:
							liveProcessingStarted?.Invoke();
							continue;
						case StreamMessage.AllStreamCheckpointReached(var position):
							checkpointReached?.Invoke(FromEsdbPosition(position));
							continue;
						case StreamMessage.Event(var resolvedEvent):
							try {
								var recorded = ToRecordedEvent(resolvedEvent);
								if (recorded is not null) {
									eventAppeared(recorded);
								}
								continue;
							} catch (Exception ex) {
								_logger.TraceException(ex, "An error occurred in the eventAppeared callback");
								subscriptionDropped?.Invoke(SubscriptionDropReasonRD.SubscribingError, ex);
								return;
							}
					}
				}
			} catch (OperationCanceledException ex) {
				established.TrySetResult();
				subscriptionDropped?.Invoke(SubscriptionDropReasonRD.UserInitiated, ex);
			} catch (Exception ex) {
				established.TrySetException(ex);
				_logger.TraceException(ex, "An error occurred in subscription callback");
				subscriptionDropped?.Invoke(SubscriptionDropReasonRD.ServerError, ex);
			}
		}, cts.Token);
		EnsureEstablished(established.Task, cts);
		return TrackSubscription(cts);
	}

	internal static IEventFilter ToClientFilter(StreamStoreEventFilter filter) => filter.Kind switch {
		StreamStoreEventFilter.FilterKind.ExcludeSystemEvents => KurrentDB.Client.EventTypeFilter.ExcludeSystemEvents(),
		StreamStoreEventFilter.FilterKind.EventTypePrefix => KurrentDB.Client.EventTypeFilter.Prefix(filter.Prefixes.ToArray()),
		StreamStoreEventFilter.FilterKind.EventTypeRegex => KurrentDB.Client.EventTypeFilter.RegularExpression(filter.Regex!),
		StreamStoreEventFilter.FilterKind.StreamPrefix => StreamFilter.Prefix(filter.Prefixes.ToArray()),
		StreamStoreEventFilter.FilterKind.StreamRegex => StreamFilter.RegularExpression(filter.Regex!),
		_ => throw new ArgumentOutOfRangeException(nameof(filter))
	};

	private static PositionRD FromEsdbPosition(EsdbPosition position) =>
		new((long)position.CommitPosition, (long)position.PreparePosition);

	private static readonly TimeSpan EstablishTimeout = TimeSpan.FromSeconds(30);

	internal enum EstablishOutcome { Established, Faulted, TimedOut }

	internal static EstablishOutcome AwaitEstablished(Task establishedTask, TimeSpan timeout) {
		try {
			return establishedTask.Wait(timeout) ? EstablishOutcome.Established : EstablishOutcome.TimedOut;
		} catch (AggregateException) {
			return EstablishOutcome.Faulted;
		}
	}

	/// <summary>
	/// Blocks until the subscription pump signals establishment. On
	/// <see cref="EstablishOutcome.TimedOut"/> or <see cref="EstablishOutcome.Faulted"/>, cancels and
	/// disposes <paramref name="cts"/> then throws so callers never receive a live disposable for a
	/// dead subscription.
	/// </summary>
	internal static void EnsureEstablished(Task establishedTask, CancellationTokenSource cts,
		TimeSpan? timeout = null) {
		var outcome = AwaitEstablished(establishedTask, timeout ?? EstablishTimeout);
		if (outcome == EstablishOutcome.Established) {
			return;
		}

		try {
			cts.Cancel();
		} catch (ObjectDisposedException) {
		}
		try {
			cts.Dispose();
		} catch (ObjectDisposedException) {
		}

		if (outcome == EstablishOutcome.TimedOut) {
			throw new TimeoutException(
				$"Subscription was not established within {(timeout ?? EstablishTimeout).TotalSeconds:0} seconds.");
		}

		throw new InvalidOperationException(
			"Subscription failed to establish.",
			establishedTask.Exception?.GetBaseException());
	}

	/// <inheritdoc cref="IStreamStoreConnection"/>
	public void DeleteStream(string stream, long expectedVersion, UserCredentialsRD? credentials = null) {
		RejectPerCallCredentials(credentials);
		try {
			_client.DeleteAsync(stream, ToStreamState(expectedVersion))
				.GetAwaiter().GetResult();
		} catch (KurrentDB.Client.WrongExpectedVersionException ex) {
			throw new WrongExpectedVersionExceptionRD(stream, (int)ex.ActualStreamState.ToInt64(), (int)expectedVersion,
				ex);
		}
	}

	/// <inheritdoc cref="IStreamStoreConnection"/>
	public void HardDeleteStream(string stream, long expectedVersion, UserCredentialsRD? credentials = null) {
		RejectPerCallCredentials(credentials);
		try {
			_client.TombstoneAsync(stream, ToStreamState(expectedVersion))
				.GetAwaiter().GetResult();
		} catch (KurrentDB.Client.WrongExpectedVersionException ex) {
			throw new WrongExpectedVersionExceptionRD(stream, (int)ex.ActualStreamState.ToInt64(), (int)expectedVersion,
				ex);
		}
	}

	private static EsdbEventData ToEventData(EventDataRD eventData) =>
		new(Uuid.FromGuid(eventData.EventId), eventData.EventType,
			eventData.Data,
			eventData.Metadata,
			eventData.IsJson ? "application/json" : "application/octet-stream");

	/// <summary>
	/// Maps a KurrentDB resolved delivery to a ReactiveDomain <see cref="RecordedEvent"/>, tagging
	/// link-resolved deliveries as <see cref="ProjectedEvent"/> and carrying <see cref="RecordedEvent.Position"/>
	/// when the backend supplies <c>OriginalPosition</c>. Returns null when the resolved event is missing
	/// (deleted/scavenged link target).
	/// </summary>
	internal static RecordedEventRD? ToRecordedEvent(ResolvedEvent resolvedEvent) {
		// KurrentDB annotates Event as non-null, but it can be null for deleted/scavenged link targets.
		EventRecord? evt = resolvedEvent.Event;
		if (evt is null) {
			return null;
		}

		var createdEpoch = ((DateTimeOffset)evt.Created).ToUnixTimeMilliseconds();
		var position = resolvedEvent.OriginalPosition is { } p
			? new PositionRD((long)p.CommitPosition, (long)p.PreparePosition)
			: (PositionRD?)null;

		if (resolvedEvent.Link is not null) {
			return new ProjectedEvent(
				resolvedEvent.Link.EventStreamId,
				evt.EventNumber.ToInt64(),
				evt.EventStreamId,
				evt.EventId.ToGuid(),
				resolvedEvent.OriginalEventNumber.ToInt64(),
				evt.EventType,
				evt.Data.ToArray(),
				evt.Metadata.ToArray(),
				evt.ContentType == "application/json",
				evt.Created,
				createdEpoch,
				position);
		}

		return new RecordedEventRD(evt.EventStreamId, evt.EventId.ToGuid(),
			resolvedEvent.OriginalEventNumber.ToInt64(),
			evt.EventType,
			evt.Data.ToArray(),
			evt.Metadata.ToArray(),
			evt.ContentType == "application/json",
			evt.Created,
			createdEpoch,
			position);
	}

	private static EsdbPosition ToEsdbPosition(PositionRD position) =>
		new((ulong)position.CommitPosition, (ulong)position.PreparePosition);

	private static StreamState ToStreamState(long expectedVersion) {
		ArgumentOutOfRangeException.ThrowIfLessThan(expectedVersion, -3);

		return expectedVersion switch {
			-2 => StreamState.Any,
			-1 => StreamState.NoStream,
			>= 0L => StreamState.StreamRevision((ulong)expectedVersion),
			_ => throw new UnreachableException()
		};
	}

	private sealed class SubscriptionDisposable(CancellationTokenSource cts) : IDisposable {
		private int _disposed;

		public void Dispose() {
			if (Interlocked.Exchange(ref _disposed, 1) != 0) {
				return;
			}
			try {
				cts.Cancel();
			} catch (ObjectDisposedException) {
			}
			try {
				cts.Dispose();
			} catch (ObjectDisposedException) {
			}
		}
	}
}
