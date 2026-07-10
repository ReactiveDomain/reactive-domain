using ReactiveDomain.Messaging;
using Xunit;

namespace ReactiveDomain.Testing;

public static class MessageListExtensions {
	/// <param name="messages">The list of messages to check.</param>
	extension(IList<IMessage> messages) {
		/// <summary>
		/// Treats the list like a queue. Pops the first message off and compares its CorrelationId with the provided one.
		/// Throws if they do not match.
		/// </summary>
		/// <typeparam name="TMsg">The expected type of the first message in the list.</typeparam>
		/// <param name="correlationId">The expected correlation ID of the first message in the list.</param>
		/// <returns>The list of messages with the first item in the list removed.</returns>
		/// <exception cref="Exception">Thrown if either the type or correlation ID do not match.</exception>
		/// <exception cref="InvalidOperationException">Thrown if the list is empty.</exception>
		public IList<IMessage> AssertNext<TMsg>(Guid correlationId)
			where TMsg : ICorrelatedMessage {
			var msg = messages.DequeueNext<TMsg>();
			if (msg.CorrelationId != correlationId)
				throw new Exception(
					$"Message type <{typeof(TMsg).Name}> found with incorrect correlationId. Expected [{correlationId}] found [{msg.CorrelationId}] instead.");
			return messages;
		}

		/// <summary>
		/// Treats the list like a queue. Pops the first message off and compares its CorrelationId with the provided one.
		/// Throws if they do not match. The popped message is provided as output.
		/// </summary>
		/// <typeparam name="TMsg">The expected type of the first message in the list.</typeparam>
		/// <param name="correlationId">The expected correlation ID of the first message in the list.</param>
		/// <param name="msg">The message that is being checked</param>
		/// <returns>The list of messages with the first item in the list removed.</returns>
		/// <exception cref="Exception">Thrown if either the type or correlation ID do not match.</exception>
		/// <exception cref="InvalidOperationException">Thrown if the list is empty.</exception>
		public IList<IMessage> AssertNext<TMsg>(Guid correlationId, out TMsg? msg)
			where TMsg : ICorrelatedMessage, IMessage {
			msg = messages.DequeueNext<TMsg>();
			if (msg.CorrelationId != correlationId)
				throw new Exception(
					$"Message type <{typeof(TMsg).Name}> found with incorrect correlationId. Expected [{correlationId}] found [{msg.CorrelationId}] instead.");
			return messages;
		}

		/// <summary>
		/// Treats the list like a queue. Pops the first message off and determines if the provided condition is true
		/// when operating on that message. Throws if the condition is false.
		/// </summary>
		/// <typeparam name="TMsg">The expected type of the first message in the list.</typeparam>
		/// <param name="condition">A condition that is expected to be true for the first message in the list.</param>
		/// <param name="userMessage">An optional message to put on the exception that's thrown if the condition is false.</param>
		/// <returns>The list of messages with the first item in the list removed.</returns>
		/// <exception cref="Exception">Thrown if either the type or correlation ID do not match.</exception>
		/// <exception cref="InvalidOperationException">Thrown if the list is empty.</exception>
		public IList<IMessage> AssertNext<TMsg>(Func<TMsg, bool> condition,
			string? userMessage = null) where TMsg : ICorrelatedMessage, IMessage {
			var msg = messages.DequeueNext<TMsg>();
			Assert.True(condition(msg), userMessage);
			return messages;
		}

		/// <summary>
		/// Asserts that the specified list of messages is empty.
		/// </summary>
		/// <exception cref="Exception">Thrown if the list of messages is not empty.</exception>
		public void AssertEmpty() {
			if (messages.Count != 0)
				throw new Exception($"List of messages is not empty. Instead {messages[0].GetType()} is next.");
		}

		/// <summary>
		/// Removes and returns the message from the beginning of the list if it matches the expected type.
		/// </summary>
		/// <remarks>This method modifies the input list by removing its first element. Use this method when
		/// message order and type are important, and ensure that the expected type is at the front of the list.</remarks>
		/// <typeparam name="TMsg">The type of message to dequeue. Must implement the IMessage interface.</typeparam>
		/// <returns>The first message in the list if it is of type <see cref="TMsg"/>.</returns>
		/// <exception cref="InvalidOperationException">Thrown if the messages list is empty.</exception>
		/// <exception cref="Exception">Thrown if the first message in the list is not of type <see cref="TMsg"/>.</exception>
		public TMsg DequeueNext<TMsg>() where TMsg : IMessage {
			if (messages.Count == 0)
				throw new InvalidOperationException("The list is empty");
			var msg = messages.Take(1).First();
			if (msg is TMsg typedMsg) {
				messages.RemoveAt(0);
				return typedMsg;
			}
			throw new Exception($"Type <{typeof(TMsg).Name}> is not next item, instead <{msg.GetType().Name}> found.");
		}
	}
}
