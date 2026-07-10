using System.Diagnostics;
using ReactiveDomain.Messaging.Bus;

namespace ReactiveDomain.Messaging;

public class PublishEnvelope : IEnvelope {
	private readonly IPublisher _publisher;
	private readonly int _createdOnThread;

	public PublishEnvelope(IPublisher publisher, bool crossThread = false) {
		_publisher = publisher;
		_createdOnThread = crossThread ? -1 : Environment.CurrentManagedThreadId;
	}

	public void ReplyWith<T>(T message) where T : IMessage {
		Debug.Assert(_createdOnThread == -1 ||
					 Environment.CurrentManagedThreadId == _createdOnThread || _publisher is IThreadSafePublisher);
		_publisher.Publish(message);
	}
}
