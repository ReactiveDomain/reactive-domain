using System;
using ReactiveDomain.Util;

namespace ReactiveDomain.Messaging
{
    public class CallbackEnvelope : IEnvelope
    {
        private readonly Action<IMessage> _callback;

        public CallbackEnvelope(Action<IMessage> callback)
        {
            _callback = callback;
            Ensure.NotNull(callback, "callback");
        }

        public void ReplyWith<T>(T message) where T : IMessage
        {
            _callback(message);
        }
    }
}
