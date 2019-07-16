using System;
using System.Collections.Generic;
using ReactiveDomain.Util;

namespace ReactiveDomain.Messaging.Bus
{
    public class IdempotentHandler<T> : IHandle<T> where T : class, IMessage
    {
        private readonly IHandle<T> _handler;
        private readonly int _bufferSize;
        private readonly Queue<Guid> _guidQueue;
        private readonly HashSet<Guid> _guids;
        private Guid _last;


        //n.b. keep the buffer size small as this is scanned on every execution
        public IdempotentHandler(IHandle<T> handle, int bufferSize = 1)
        {
            Ensure.NotNull(handle, "handle");
            Ensure.GreaterThan(0, bufferSize, nameof(bufferSize));
            _handler = handle;
            _bufferSize = bufferSize;
            _guidQueue = new Queue<Guid>(bufferSize);
            _guids = new HashSet<Guid>();
            _last = Guid.Empty;
        }

        public void Handle(T message)
        {
            var id = message.MsgId;

            if (_last == id || _guids.Contains(id)) return;
            _last = id;

            if (_bufferSize > 1)
            {
                if (_guidQueue.Count >= _bufferSize)
                    _guids.Remove(_guidQueue.Dequeue());
                _guids.Add(id);
                _guidQueue.Enqueue(id);
            }
            _handler.Handle(message);
        }
    }
}