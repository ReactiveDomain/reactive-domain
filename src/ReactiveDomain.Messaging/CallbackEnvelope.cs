using System;
using ReactiveDomain.Util;

namespace ReactiveDomain.Messaging
{
    public class CallbackEnvelope : IEnvelope
    {
        private readonly Action<Message> _callback;

        public CallbackEnvelope(Action<Message> callback)
        {
            _callback = callback;
            Ensure.NotNull(callback, "callback");
        }

        public void ReplyWith<T>(T message) where T : Message
        {
            _callback(message);
        }
    }
}
