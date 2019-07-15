namespace ReactiveDomain.Messaging.Bus
{
    public class DynamicHandler:IHandle<IMessage>
    {
        private readonly dynamic _target;

        public DynamicHandler(dynamic target)
        {
            _target = target;
        }

        public void Handle(IMessage message)
        {
            dynamic msg = message;
            _target.Handle(msg);
        }
    }
}
