using System;
using ReactiveDomain.Util;

namespace ReactiveDomain.Messaging.Bus
{
    public class AdHocHandler<T> : IHandle<T> where T : class, IMessage
    {
        private readonly Action<T> _handle;

        public AdHocHandler(Action<T> handle)
        {
            Ensure.NotNull(handle, "handle");
            _handle = handle;
        }

        public void Handle(T message)
        {
            _handle(message);
        }
    }
}