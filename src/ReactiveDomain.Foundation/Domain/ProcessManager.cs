using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;


// ReSharper disable once CheckNamespace
namespace ReactiveDomain;

public abstract class ProcessManager : AggregateRoot, IHandle<IMessage> {
	protected ProcessManager(ICorrelatedMessage? source = null) : base(source) {
		Register<InputMsg>(_ => {/* input messages have no apply action, saved for audit only*/ });
	}

	public abstract void Handle(IMessage message);

	protected void RecordInput(InputMsg receivedMsg) {
		Raise(new InputMsg(receivedMsg));
	}
	public record InputMsg(IMessage Received) : Message;
}
