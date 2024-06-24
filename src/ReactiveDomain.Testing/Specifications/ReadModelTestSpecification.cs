using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using System.Collections.Generic;

namespace ReactiveDomain.Testing
{
    /// <summary>
	/// A base  test class for testing read models. Includes a <see cref="NullConfiguredConnection"/>
	/// that can be used creating read models under test without needing additional infrastructure, and can be used itself as an <see cref="IPublisher"/>
	/// to collect published messages from read models. These messages are recorded in <see cref="PublishedMessages"/> for use in tests.
	/// </summary>
	public abstract class ReadModelTestSpecification : IPublisher
    {
        /// <summary>
        /// Adds the message onto the test class's list of published messages.
        /// Required for implementation of <see cref="IPublisher"/>.
        /// </summary>
        /// <param name="message">The message to add.</param>
        void IPublisher.Publish(IMessage message)
        {
            PublishedMessages.Add(message);
        }
        /// <summary>
        /// The list of messages recorded from the test class's Publish method.
        /// </summary>
        protected List<IMessage> PublishedMessages = new List<IMessage>();
        /// <summary>
        /// A <see cref="NullConfiguredConnection"/> for use in creating read models under test without needing additional infrastructure.
        /// </summary>
        protected IConfiguredConnection Connection = new NullConfiguredConnection();
    }
}
