using System;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;

namespace ReactiveDomain.Testing
{
	public class TestPublisher : IPublisher
	{
		private readonly Action<Message> _publish;

		public TestPublisher(Action<Message> publish)
		{
			_publish = publish;
		}

		public void Publish(Message msg)
		{
			_publish(msg);
		}
	}
}