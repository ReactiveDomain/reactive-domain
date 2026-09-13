using ReactiveDomain.Util;

// ReSharper disable once CheckNamespace
namespace ReactiveDomain.Foundation;

/// <summary>One stream for <see cref="ReadModelBase.StartAllAsync"/> to start.</summary>
/// <remarks>
/// Build one with <see cref="Named"/>, <see cref="ForAggregate{TAggregate}"/> or
/// <see cref="ForCategory{TAggregate}"/>. The stream name is resolved against the model's own
/// connection when the model starts it, so a starter can be built before any model exists.
/// </remarks>
public sealed record StreamStarter {
	private StreamStarter() { }

	private string? Name { get; init; }
	private Type? Aggregate { get; init; }
	private Guid? Id { get; init; }

	/// <summary>The event to resume after, or null to read the stream from its first.</summary>
	public long? Checkpoint { get; init; }

	/// <summary>Whether the stream must already exist when the listener attaches.</summary>
	public bool ValidateStream { get; init; }

	/// <summary>A stream named directly, for a stream the naming convention does not cover.</summary>
	/// <param name="stream">The stream's name in the store.</param>
	/// <param name="checkpoint">The event to resume after.</param>
	/// <param name="validateStream">Require the stream to exist.</param>
	public static StreamStarter Named(string stream, long? checkpoint = null, bool validateStream = false) {
		Ensure.NotNullOrEmpty(stream, nameof(stream));
		return new StreamStarter { Name = stream, Checkpoint = checkpoint, ValidateStream = validateStream };
	}

	/// <summary>One aggregate's own stream.</summary>
	/// <typeparam name="TAggregate">The aggregate whose stream to read.</typeparam>
	/// <param name="id">The aggregate's id.</param>
	/// <param name="checkpoint">The event to resume after.</param>
	/// <param name="validateStream">Require the stream to exist.</param>
	public static StreamStarter ForAggregate<TAggregate>(
		Guid id,
		long? checkpoint = null,
		bool validateStream = false) where TAggregate : class, IEventSource =>
		new() { Aggregate = typeof(TAggregate), Id = id, Checkpoint = checkpoint, ValidateStream = validateStream };

	/// <summary>The category stream carrying every <typeparamref name="TAggregate"/>.</summary>
	/// <typeparam name="TAggregate">The aggregate type whose category to read.</typeparam>
	/// <param name="checkpoint">The event to resume after.</param>
	/// <param name="validateStream">Require the stream to exist.</param>
	public static StreamStarter ForCategory<TAggregate>(
		long? checkpoint = null,
		bool validateStream = false) where TAggregate : class, IEventSource =>
		new() { Aggregate = typeof(TAggregate), Checkpoint = checkpoint, ValidateStream = validateStream };

	internal string StreamName(IStreamNameBuilder namer) =>
		Name ?? (Id is null
			? namer.GenerateForCategory(Aggregate!)
			: namer.GenerateForAggregate(Aggregate!, Id.Value));
}
