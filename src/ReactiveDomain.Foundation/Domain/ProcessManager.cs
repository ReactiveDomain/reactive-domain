using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;


namespace ReactiveDomain.Foundation.Domain
{
    public abstract class ProcessManager: AggregateRoot, IHandle<IMessage>
    {
        public ProcessManager(ICorrelatedMessage source = null):base(source) {
            Register<InputMsg>(msg => {/* input messages have no apply action, saved for audit only*/ });
        }

        public abstract void Handle(IMessage message);
        
        protected void RecordInput(InputMsg recievedMsg) {
            Raise(new InputMsg(recievedMsg));
        }
        public class InputMsg:Message {
            public IMessage Received { get; private set; }
            public InputMsg(IMessage recieved)
            {
                Received = recieved;
            }
        }
    }
}
