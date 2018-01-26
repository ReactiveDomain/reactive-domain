namespace ReactiveDomain.Messaging.Bus
{
    public class DynamicHandler:IHandle<Message>
    {
        private readonly dynamic _target;

        public DynamicHandler(dynamic target)
        {
            _target = target;
        }

        public void Handle(Message message)
        {
            dynamic msg = message;
            _target.Handle(msg);
        }
    }
}
