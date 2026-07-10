using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;

namespace ReactiveDomain.Testing;

public class TestPublisher(Action<IMessage> publish) : IPublisher {
	public void Publish(IMessage msg) {
		publish(msg);
	}
}
