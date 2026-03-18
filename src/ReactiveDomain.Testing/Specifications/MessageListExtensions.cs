#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ReactiveDomain.Messaging;
using Xunit;

namespace ReactiveDomain.Testing;

public static class MessageListExtensions {
    /// <summary>
    /// Treats the list like a queue. Pops the first message off and compares its CorrelationId with the provided one.
    /// Throws if they do not match.
    /// </summary>
    /// <typeparam name="TMsg">The expected type of the first message in the list.</typeparam>
    /// <param name="messages">The list of messages to check.</param>
    /// <param name="correlationId">The expected correlation ID of the first message in the list.</param>
    /// <returns>The list of messages with the first item in the list removed.</returns>
    /// <exception cref="Exception">Thrown if either the type or correlation ID do not match.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the list is empty.</exception>
    public static IList<IMessage> AssertNext<TMsg>(this IList<IMessage> messages, Guid correlationId)
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
    /// <param name="messages">The list of messages to check.</param>
    /// <param name="correlationId">The expected correlation ID of the first message in the list.</param>
    /// <param name="msg">The message that is being checked</param>
    /// <returns>The list of messages with the first item in the list removed.</returns>
    /// <exception cref="Exception">Thrown if either the type or correlation ID do not match.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the list is empty.</exception>
    public static IList<IMessage> AssertNext<TMsg>(this IList<IMessage> messages, Guid correlationId, out TMsg? msg)
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
    /// <param name="messages">The list of messages to check.</param>
    /// <param name="condition">A condition that is expected to be true for the first message in the list.</param>
    /// <param name="userMessage">An optional message to put on the exception that's thrown if the condition is false.</param>
    /// <returns>The list of messages with the first item in the list removed.</returns>
    /// <exception cref="Exception">Thrown if either the type or correlation ID do not match.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the list is empty.</exception>
    public static IList<IMessage> AssertNext<TMsg>(this IList<IMessage> messages, Func<TMsg, bool> condition,
        string? userMessage = null) where TMsg : ICorrelatedMessage, IMessage {
        var msg = messages.DequeueNext<TMsg>();
        Assert.True(condition(msg), userMessage);
        return messages;
    }

    /// <summary>
    /// Asserts that the specified list of messages is empty.
    /// </summary>
    /// <param name="messages">The list of messages to check for emptiness. Cannot be null.</param>
    /// <exception cref="Exception">Thrown if the list of messages is not empty.</exception>
    public static void AssertEmpty(this IList<IMessage> messages) {
        if (messages.Count != 0)
            throw new Exception($"List of messages is not empty. Instead {messages[0].GetType()} is next.");
    }

    /// <summary>
    /// Removes and returns the message from the beginning of the list if it matches the expected type.
    /// </summary>
    /// <remarks>This method modifies the input list by removing its first element. Use this method when
    /// message order and type are important, and ensure that the expected type is at the front of the list.</remarks>
    /// <typeparam name="TMsg">The type of message to dequeue. Must implement the IMessage interface.</typeparam>
    /// <param name="messages">The list of messages to dequeue from. The first item must be of type TMsg.</param>
    /// <returns>The first message in the list if it is of type <see cref="TMsg"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the messages list is empty.</exception>
    /// <exception cref="Exception">Thrown if the first message in the list is not of type <see cref="TMsg"/>.</exception>
    public static TMsg DequeueNext<TMsg>(this IList<IMessage> messages) where TMsg : IMessage {
        if (messages.Count == 0)
            throw new InvalidOperationException("The list is empty");
        var msg = messages.Take(1).First();
        if (msg is TMsg typedMsg) {
            messages.RemoveAt(0);
            return typedMsg;
        }
        throw new Exception($"Type <{typeof(TMsg).Name}> is not next item, instead <{msg.GetType().Name}> found.");
    }

    /// <summary>
    /// Blocks the calling thread until a message of the specified type is present in the collection or the timeout
    /// elapses.
    /// </summary>
    /// <remarks>This method blocks the calling thread until the specified condition is met or the timeout
    /// expires. The method uses a spin-wait, which may impact CPU usage if the wait duration is long.</remarks>
    /// <typeparam name="TMsg">The type of message to wait for. Must implement the IMessage interface.</typeparam>
    /// <param name="messages">The collection of messages to monitor for the specified message type. Cannot be null.</param>
    /// <param name="timeout">The maximum duration to wait for a message of the specified type to appear.</param>
    /// <exception cref="TimeoutException">Thrown if a message of the specified type does not appear in the collection before the timeout expires.</exception>
    public static void WaitFor<TMsg>(this IList<IMessage> messages, TimeSpan timeout) where TMsg : IMessage {
        var result = SpinWait.SpinUntil(() => messages.Any(x => x is TMsg), timeout);
        if (!result)
            throw new TimeoutException();
    }

    /// <summary>
    /// Waits until at least the specified number of messages of type TMsg are present in the collection or the timeout
    /// elapses.
    /// </summary>
    /// <remarks>This method blocks the calling thread until the specified condition is met or the timeout
    /// expires. The method uses a spin-wait, which may impact CPU usage if the wait duration is long.</remarks>
    /// <typeparam name="TMsg">The type of message to wait for. Must implement the IMessage interface.</typeparam>
    /// <param name="messages">The collection of messages to monitor for instances of type TMsg. Cannot be null.</param>
    /// <param name="num">The minimum number of messages of type TMsg to wait for. Must be greater than or equal to zero.</param>
    /// <param name="timeout">The maximum duration to wait for the required number of messages to appear.</param>
    /// <exception cref="TimeoutException">Thrown if the required number of messages of type TMsg are not present in the collection before the timeout
    /// elapses.</exception>
    public static void WaitForMultiple<TMsg>(this IList<IMessage> messages, uint num, TimeSpan timeout) where TMsg : IMessage {
        var result = SpinWait.SpinUntil(() => messages.Count(x => x is TMsg) >= num, timeout);
        if (!result)
            throw new TimeoutException();
    }
}
