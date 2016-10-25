using System;
using System.Threading.Tasks;
using NLog;
using ReactiveDomain.Messaging;

namespace ReactiveDomain.Bus
{
    public class CommandHandler<T> : IHandle<T> where T : Command
    {
        private static readonly Logger Log = LogManager.GetLogger("ReactiveDomain");
        private readonly IPublisher _bus;
        private readonly IHandleCommand<T> _handler;
        public AdHocHandler<CancelCommand> CancelHandler; 
        public CommandHandler(IPublisher bus, IHandleCommand<T> handler)
        {
            _bus = bus;
            _handler = handler;
            CancelHandler = new AdHocHandler<CancelCommand>(RequestCancel);
        }

        public void Handle(T message)
        {

            Task.Run(() =>
            {
                _bus.Publish(new AckCommand(message));
                try
                {
                   if(Log.IsDebugEnabled)
                        Log.Debug("{0} command handled by {1}", message.GetType().Name, _handler.GetType().Name);
                    _bus.Publish(_handler.Handle(message));
                }
                catch (Exception ex)
                {
                    _bus.Publish(message.Fail(ex));
                }
            });
        }


        public void RequestCancel(CancelCommand message)
        {
            try
            {
                _handler.RequestCancel(message);
            }
            catch (Exception)
            {
            }
        }
    }
}