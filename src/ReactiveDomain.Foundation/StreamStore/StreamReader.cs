using ReactiveDomain.Logging;
using ReactiveDomain.Messaging;
using ReactiveDomain.Util;

// ReSharper disable once CheckNamespace
namespace ReactiveDomain.Foundation;

/// <summary>
/// This class reads streams and is primarily used in the building of read models. 
/// The Raw events returned from the Stream will be unwrapped using the provided serializer and
/// handed to the injected handler.
///</summary>
public class StreamReader : IStreamReader {
	protected readonly string ReaderName;
	private readonly IStreamNameBuilder _streamNameBuilder;
	protected readonly IEventSerializer Serializer;
	private readonly IStreamStoreConnection _streamStoreConnection;
	protected long StreamPosition;
	protected bool FirstEventRead;
	private bool _hasRead;
	private long? _resumeAt;
	private static readonly ILogger Log = LogManager.GetLogger("ReactiveDomain");

	/// <inheritdoc cref="IStreamReader.Position"/>
	public long? Position {
		get {
			if (!_hasRead)
				return null;
			return FirstEventRead ? StreamPosition : _resumeAt;
		}
	}

	private readonly object _checkpointLock = new();
	private Position? _allPosition;

	/// <inheritdoc cref="IStreamReader.Checkpoint"/>
	public StreamCheckpoint? Checkpoint {
		get {
			lock (_checkpointLock) {
				if (!_hasRead || string.IsNullOrEmpty(StreamName))
					return null;
				return new StreamCheckpoint(
					StreamName,
					FirstEventRead ? Interlocked.Read(ref StreamPosition) : _resumeAt,
					FirstEventRead ? _allPosition : null);
			}
		}
	}
	private Action<IMessage> _handle;
	private Action<IMessage, StreamCheckpoint?>? _pairedHandle;

	/// <summary>The target for read events. Setting it replaces a <see cref="PairedHandle"/> as well.</summary>
	public Action<IMessage> Handle {
		get => _handle;
		set {
			_handle = value;
			_pairedHandle = null;
		}
	}

	/// <inheritdoc cref="IStreamReader.PairedHandle"/>
	public Action<IMessage, StreamCheckpoint?> PairedHandle {
		set => _pairedHandle = value;
	}

	public string StreamName { get; private set; } = string.Empty;
	private const int ReadPageSize = 500;

	private bool _cancelled;

	/// <summary>
	/// Create a stream Reader
	/// </summary>
	/// <param name="name">Name of the reader</param>
	/// <param name="streamStoreConnection">The stream store to subscribe to</param>
	/// <param name="streamNameBuilder">The source for correct stream names based on aggregates and events</param>
	/// <param name="serializer">the serializer to apply to the events in the stream</param>
	/// <param name="handle">The target handle that read events are passed to</param>
	public StreamReader(
		string? name,
		IStreamStoreConnection streamStoreConnection,
		IStreamNameBuilder streamNameBuilder,
		IEventSerializer serializer,
		Action<IMessage> handle) {
		ReaderName = name ?? nameof(StreamReader);
		_streamStoreConnection = streamStoreConnection ?? throw new ArgumentNullException(nameof(streamStoreConnection));
		_streamNameBuilder = streamNameBuilder ?? throw new ArgumentNullException(nameof(streamNameBuilder));
		Serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
		_handle = handle;
	}

	/// <summary>
	/// By Event Type Projection Reader
	/// i.e. $et-[MessageType]
	/// </summary>
	/// <param name="tMessage">The message type used to generate the stream (projection) name</param>
	/// <param name="completionCheck">Read will block until true to ensure processing has completed, use '()=> true' to continue without blocking. If cancellation or timeout is required it should be implemented in the completion method</param>
	/// <param name="checkpoint">The event number of the last received event. Reading will start with the next event.</param>
	/// <param name="count">The count of items to read</param>
	/// <param name="readBackwards">Read the stream backwards</param>
	/// <returns>Returns true if any events were read from the stream</returns>
	/// <exception cref="ArgumentException"><paramref name="tMessage"/> must implement IMessage</exception>
	public bool Read(
		Type tMessage,
		Func<bool> completionCheck,
		long? checkpoint = null,
		long? count = null,
		bool readBackwards = false) {
		if (!typeof(IMessage).IsAssignableFrom(tMessage)) {
			throw new ArgumentException("tMessage must implement IMessage", nameof(tMessage));
		}

		return Read(
			_streamNameBuilder.GenerateForEventType(tMessage.Name),
			completionCheck,
			checkpoint,
			count,
			readBackwards);
	}

	/// <summary>
	/// By Category Projection Stream Reader
	/// i.e. $ce-[AggregateType]
	/// </summary>
	/// <typeparam name="TAggregate">The Aggregate type used to generate the stream name</typeparam>
	/// <param name="completionCheck">Read will block until true to ensure processing has completed, use '()=> true' to continue without blocking. If cancellation or timeout is required it should be implemented in the completion method</param>
	/// <param name="checkpoint">The event number of the last received event. Reading will start with the next event.</param>
	/// <param name="count">The count of items to read</param>
	/// <param name="readBackwards">Read the stream backwards</param>
	/// <returns>Returns true if any events were read from the stream</returns>
	public bool Read<TAggregate>(
		Func<bool> completionCheck,
		long? checkpoint = null,
		long? count = null,
		bool readBackwards = false) where TAggregate : class, IEventSource {
		return Read(
			_streamNameBuilder.GenerateForCategory(typeof(TAggregate)),
			completionCheck,
			checkpoint,
			count,
			readBackwards);
	}


	/// <summary>
	/// Aggregate-[id] Stream Reader
	/// i.e. [AggregateType]-[id]
	/// </summary>
	/// <typeparam name="TAggregate">The Aggregate type used to generate the stream name</typeparam>
	/// <param name="id">Aggregate id to generate stream name.</param>
	/// <param name="completionCheck">Read will block until true to ensure processing has completed, use '()=> true' to continue without blocking. If cancellation or timeout is required it should be implemented in the completion method</param>
	/// <param name="checkpoint">The event number of the last received event. Reading will start with the next event.</param>
	/// <param name="count">The count of items to read</param>
	/// <param name="readBackwards">Read the stream backwards</param>
	/// <returns>Returns true if any events were read from the stream</returns>
	public bool Read<TAggregate>(
		Guid id,
		Func<bool> completionCheck,
		long? checkpoint = null,
		long? count = null,
		bool readBackwards = false) where TAggregate : class, IEventSource {
		return Read(
			_streamNameBuilder.GenerateForAggregate(typeof(TAggregate), id),
			completionCheck,
			checkpoint,
			count,
			readBackwards);
	}

	/// <summary>
	/// Named Stream Reader
	/// i.e. [StreamName]
	/// </summary>
	/// <param name="streamName">An exact stream name.</param>
	/// <param name="completionCheck">Read will block until true to ensure processing has completed, use '<c>() => true</c>' to continue without blocking. If cancellation or timeout is required it should be implemented in the completion method</param>
	/// <param name="checkpoint">The event number of the last received event. Reading will start with the next event.</param>
	/// <param name="count">The count of items to read</param>
	/// <param name="readBackwards">Read the stream backwards</param>
	/// <returns>Returns true if any events were read from the stream</returns>
	public virtual bool Read(
		string streamName,
		Func<bool> completionCheck,
		long? checkpoint = null,
		long? count = null,
		bool readBackwards = false) {
		if (checkpoint != null)
			Ensure.Nonnegative((long)checkpoint, nameof(checkpoint));
		if (count != null)
			Ensure.Positive((long)count, nameof(count));
		if (!ValidateStreamName(streamName))
			return false;

		_cancelled = false;
		FirstEventRead = false;
		_resumeAt = checkpoint;
		_hasRead = true;
		StreamName = streamName;
		long sliceStart;
		if (checkpoint is null)
			sliceStart = readBackwards ? -1 : 0;
		else
			sliceStart = checkpoint.Value + (readBackwards ? -1 : 1);
		long remaining = count ?? long.MaxValue;
		StreamEventsSlice? currentSlice;

		do {
			var page = remaining < ReadPageSize ? remaining : ReadPageSize;

			currentSlice = !readBackwards
				? _streamStoreConnection.ReadStreamForward(streamName, sliceStart, page)
				: _streamStoreConnection.ReadStreamBackward(streamName, sliceStart, page);

			if (currentSlice?.Events.Length > 0) { FirstEventRead = true; }
			if (currentSlice is not null) {
				remaining -= currentSlice.Events.Length;
				sliceStart = currentSlice.NextEventNumber;
			}

			Array.ForEach(currentSlice?.Events ?? [], EventRead);

		} while (currentSlice?.IsEndOfStream == false && !_cancelled && remaining != 0);
		if (FirstEventRead) { SpinWait.SpinUntil(() => { try { return completionCheck(); } catch { return true; } }); }
		return FirstEventRead;
	}

	/// <summary>
	/// Determines whether the string is the name of an existing stream.
	/// </summary>
	/// <param name="streamName">The stream name to validate.</param>
	public bool ValidateStreamName(string streamName) {
		try {
			var result = _streamStoreConnection.ReadStreamForward(streamName, 0, 1);

			return result?.GetType() == typeof(StreamEventsSlice);
		} catch (Exception) {
			return false;
		}

	}

	protected virtual void EventRead(RecordedEvent recordedEvent) {
		// do not publish or increase counters if cancelled
		if (_cancelled)
			return;

		// Both clocks and the read flag move together, so a reader of Checkpoint cannot see a version
		// from one event beside a position from another.
		lock (_checkpointLock) {
			Interlocked.Exchange(ref StreamPosition, recordedEvent.EventNumber);
			_allPosition = recordedEvent.Position;
			FirstEventRead = true;
		}

		if (TryDeserialize(recordedEvent) is not { } @event)
			return;
		if (_pairedHandle is { } paired)
			paired(@event, new StreamCheckpoint(StreamName, recordedEvent.EventNumber, recordedEvent.Position));
		else
			_handle(@event);
	}

	private IMessage? TryDeserialize(RecordedEvent recordedEvent) {
		object? deserialized;
		try {
			deserialized = Serializer.Deserialize(recordedEvent);
		} catch (Exception ex) {
			Log.ErrorException(
				ex,
				"Failed to deserialize {0} #{1} ({2}); skipping so the read can continue.",
				StreamName, recordedEvent.EventNumber, recordedEvent.EventType);
			return null;
		}
		if (deserialized is IMessage message)
			return message;
		if (deserialized is not null) {
			Log.Error(
				"Dropped {0} #{1} ({2}): deserialized to {3}, not IMessage.",
				StreamName, recordedEvent.EventNumber, recordedEvent.EventType, deserialized.GetType().FullName);
		}
		return null;
	}

	/// <summary>
	/// Cancels reading the stream.
	/// </summary>
	public void Cancel() {
		_cancelled = true;
	}

	#region Implementation of IDisposable
	private bool _disposed;
	public void Dispose() {
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	protected virtual void Dispose(bool disposing) {
		if (_disposed)
			return;
		_disposed = true;
	}
	#endregion
}
