using ReactiveDomain.Messaging;

// ReSharper disable once CheckNamespace
namespace ReactiveDomain.Foundation;

public class StreamStoreMsgs {
	public record CatchupSubscriptionBecameLive : IMessage {
		public Guid MsgId { get; private set; } = Guid.NewGuid();
	}
}
