using System;

using Microsoft.Extensions.Logging;

namespace ReactiveDomain.Messaging.Bus
{
    public class CommandHandler<T> : IHandle<T> where T : class, ICommand
    {
        // ReSharper disable once StaticMemberInGenericType
        private static readonly ILogger Log = Logging.LogProvider.GetLogger("ReactiveDomain");
        private readonly IPublisher _bus;
        private readonly IHandleCommand<T> _handler;
        public CommandHandler(IPublisher bus, IHandleCommand<T> handler)
        {
            _bus = bus;
            _handler = handler;
        }

        public void Handle(T message)
        {
            _bus.Publish(new AckCommand(message));
            try
            {
                //if (Log.LogLevel >= LogLevel.Debug)
                //    Log.Debug("{0} command handled by {1}", message.GetType().Name, _handler.GetType().Name);
                Log.LogDebug("{0} command handled by {1}", message.GetType().Name, _handler.GetType().Name);
                _bus.Publish(_handler.Handle(message));
            }
            catch (Exception ex)
            {
                _bus.Publish(message.Fail(ex));
            }
        }
    }
}