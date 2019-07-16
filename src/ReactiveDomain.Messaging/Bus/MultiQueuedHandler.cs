// ReSharper disable ForCanBeConvertedToForeach
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedMember.Global

using System;
using System.Threading;
using System.Threading.Tasks;
using ReactiveDomain.Util;

namespace ReactiveDomain.Messaging.Bus
{
    public class MultiQueuedHandler : IHandle<IMessage>, IPublisher, IThreadSafePublisher
    {
        private readonly int _queueCount;

        public bool Idle {
            get {
                for (var i = 0; i < _queueCount; i++) {
                    if (!Queues[i].Idle) return false;
                }
                return true;
            }
        }
        public readonly IQueuedHandler[] Queues;

        private readonly Func<IMessage, int> _queueHash;
        private int _nextQueueNum = -1;

        public MultiQueuedHandler(int queueCount,
                                  Func<int, IQueuedHandler> queueFactory,
                                  Func<IMessage, int> queueHash = null)
        {
            _queueCount = queueCount;
            Ensure.Positive(queueCount, "queueCount");
            Ensure.NotNull(queueFactory, "queueFactory");

            Queues = new IQueuedHandler[queueCount];
            for (var i = 0; i < Queues.Length; ++i)
            {
                Queues[i] = queueFactory(i);
            }
            _queueHash = queueHash ?? NextQueueHash;
        }
        // ReSharper disable once CoVariantArrayConversion
        public MultiQueuedHandler(params QueuedHandler[] queues)
            : this(queues, null)
        {
            Ensure.Positive(queues.Length, "queues.Length");
        }

        public MultiQueuedHandler(IQueuedHandler[] queues, Func<IMessage, int> queueHash)
        {
            Ensure.NotNull(queues, "queues");
            Ensure.Positive(queues.Length, "queues.Length");

            Queues = queues;
            _queueHash = queueHash ?? NextQueueHash;
        }

        private int NextQueueHash(IMessage msg)
        {
            return Interlocked.Increment(ref _nextQueueNum);
        }

        public void Start()
        {
            for (var i = 0; i < Queues.Length; ++i)
            {
                Queues[i].Start();
            }
        }

        public void Stop()
        {
            var stopTasks = new Task[Queues.Length];
            for (var i = 0; i < Queues.Length; ++i)
            {
                var queueNum = i;
                stopTasks[i] = Task.Factory.StartNew(() => Queues[queueNum].Stop());
            }
            Task.WaitAll(stopTasks);
        }

        public void Handle(IMessage message)
        {
            Publish(message);
        }

        public void Publish(IMessage message)
        {
            // ReSharper disable once SuspiciousTypeConversion.Global
            var affineMsg = message as IQueueAffineMessage;
            var queueHash = affineMsg?.QueueId ?? _queueHash(message);
            var queueNum = (int)((uint)queueHash % Queues.Length);
            Queues[queueNum].Publish(message);
        }

        public void PublishToAll(IMessage message)
        {
            for (var i = 0; i < Queues.Length; ++i)
            {
                Queues[i].Publish(message);
            }
        }
    }
}