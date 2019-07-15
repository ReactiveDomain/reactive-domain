using System;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;

namespace ReactiveDomain.Testing
{
	public class TestPublisher : IPublisher
	{
		private readonly Action<IMessage> _publish;

		public TestPublisher(Action<IMessage> publish)
		{
			_publish = publish;
		}

		public void Publish(IMessage msg)
		{
			_publish(msg);
		}
	}
}