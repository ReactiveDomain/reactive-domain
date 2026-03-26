#nullable enable
using System;
using System.Collections.Generic;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;

namespace ReactiveDomain.Testing.Messaging;

/// <summary>
/// Extends <see cref="NullBus"/> to capture <see cref="Send"/> and <see cref="Publish"/> calls
/// for test assertions. Zero threads created. Subscribing clients will not get messages delivered.
/// </summary>
public class CapturingBus : NullBus {
	public List<ICommand> SentCommands { get; } = [];
	public List<IMessage> PublishedMessages { get; } = [];

	/// <summary>
	/// When true, sent commands fail instead of succeeding,
	/// allowing tests to verify error-handling paths.
	/// </summary>
	public bool FailOnSend { get; set; }

	public override void Publish(IMessage message) {
		PublishedMessages.Add(message);
	}

	public override void Send(ICommand command, string? exceptionMsg = null, TimeSpan? responseTimeout = null, TimeSpan? ackTimeout = null) {
		SentCommands.Add(command);
		if (FailOnSend)
			throw new Exception("Command failed");
		command.Succeed();
	}

	public override bool TrySend(ICommand command, out CommandResponse response, TimeSpan? responseTimeout = null, TimeSpan? ackTimeout = null) {
		SentCommands.Add(command);
		if (FailOnSend) {
			response = command.Fail(new Exception("Command failed"));
			return false;
		}
		response = command.Succeed();
		return true;
	}

	public override bool TrySendAsync(ICommand command, TimeSpan? responseTimeout = null, TimeSpan? ackTimeout = null) {
		SentCommands.Add(command);
		if (FailOnSend)
			return false;
		command.Succeed();
		return true;
	}
}
