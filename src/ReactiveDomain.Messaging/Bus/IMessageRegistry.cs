namespace ReactiveDomain.Messaging.Bus;

/// <summary>
/// Reports the message types a bus, read model or subscriber is subscribed to.
/// </summary>
/// <remarks>
/// <para>Separate from <see cref="ISubscriber"/> so that reporting registrations does not oblige
/// every subscriber to be able to answer. Implement it where the registrations are actually
/// held.</para>
/// <para>Both members return a fresh snapshot on each read; the registrations behind them change
/// only through a <c>Subscribe</c> call and the disposal of what it returned.</para>
/// </remarks>
public interface IMessageRegistry {
	/// <summary>
	/// The types that were subscribed to: one entry per distinct <c>T</c> passed to a
	/// <c>Subscribe</c> overload.
	/// </summary>
	/// <remarks>
	/// This is what the caller asked for, not what it receives. A registration made with
	/// <c>includeDerived: true</c> — the default — also routes every type derived from
	/// <c>T</c>, and those do not appear here. For what actually arrives, read
	/// <see cref="HandledMessageTypes"/>; the difference between the two sets is exactly what
	/// <c>includeDerived</c> added.
	/// </remarks>
	IReadOnlyCollection<Type> RegisteredMessageTypes { get; }

	/// <summary>
	/// The types that reach a handler here, including every derived type a registration routes.
	/// </summary>
	/// <remarks>
	/// A superset of <see cref="RegisteredMessageTypes"/>, equal to it only when every registration
	/// was made with <c>includeDerived: false</c>. Answers "would this component see such a
	/// message"; <see cref="RegisteredMessageTypes"/> answers "what did it ask for".
	/// </remarks>
	IReadOnlyCollection<Type> HandledMessageTypes { get; }
}
