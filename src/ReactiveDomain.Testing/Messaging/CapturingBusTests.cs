using System;
using ReactiveDomain.Messaging;
using Xunit;

namespace ReactiveDomain.Testing.Messaging;

public sealed class CapturingBusTests {
	private readonly CapturingBus _sut = new();

	[Fact]
	public void CanCaptureMessageInPublish() {
		var msg = MessageBuilder.New(() => new TestEvent());
		_sut.Publish(msg);
		var m = Assert.Single(_sut.PublishedMessages);
		Assert.Same(msg, m);
		Assert.Empty(_sut.SentCommands);
	}

	[Fact]
	public void CanCaptureCommandInSend() {
		var cmd = MessageBuilder.New(() => new TestCommands.Command1());
		_sut.Send(cmd);
		var c = Assert.Single(_sut.SentCommands);
		Assert.Same(cmd, c);
		Assert.Empty(_sut.PublishedMessages);
	}

	[Fact]
	public void FailedCommandIsCapturedOnSend() {
		_sut.FailOnSend = true;
		var cmd = MessageBuilder.New(() => new TestCommands.Command1());
		Assert.Throws<Exception>(() => _sut.Send(cmd));
		var c = Assert.Single(_sut.SentCommands);
		Assert.Same(cmd, c);
		Assert.Empty(_sut.PublishedMessages);
	}

	[Fact]
	public void CanCaptureCommandInTrySend() {
		var cmd = MessageBuilder.New(() => new TestCommands.Command1());
		_sut.TrySend(cmd, out var response);
		Assert.IsType<Success>(response);
		var c = Assert.Single(_sut.SentCommands);
		Assert.Same(cmd, c);
		Assert.Empty(_sut.PublishedMessages);
	}

	[Fact]
	public void FailedCommandIsCapturedOnTrySend() {
		_sut.FailOnSend = true;
		var cmd = MessageBuilder.New(() => new TestCommands.Command1());
		_sut.TrySend(cmd, out var response);
		Assert.IsType<Fail>(response);
		var c = Assert.Single(_sut.SentCommands);
		Assert.Same(cmd, c);
		Assert.Empty(_sut.PublishedMessages);
	}

	[Fact]
	public void CanCaptureCommandInTrySendAsync() {
		var cmd = MessageBuilder.New(() => new TestCommands.Command1());
		_sut.TrySendAsync(cmd);
		var c = Assert.Single(_sut.SentCommands);
		Assert.Same(cmd, c);
		Assert.Empty(_sut.PublishedMessages);
	}

	[Fact]
	public void FailedCommandIsCapturedOnTrySendAsync() {
		_sut.FailOnSend = true;
		var cmd = MessageBuilder.New(() => new TestCommands.Command1());
		_sut.TrySendAsync(cmd);
		var c = Assert.Single(_sut.SentCommands);
		Assert.Same(cmd, c);
		Assert.Empty(_sut.PublishedMessages);
	}
}
