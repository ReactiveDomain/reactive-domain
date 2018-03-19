using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using ReactiveDomain.Messaging.Logging;
using ReactiveDomain.Messaging.Util;

namespace ReactiveDomain.Messaging.Bus
{

	public class CommandBus : InMemoryBus, IGeneralBus
	{
		private static readonly ILogger Log = LogManager.GetLogger("ReactiveDomain");

		private readonly Dictionary<Type, object> _handleWrappers;
		private readonly CommandPublisher _publisher;
		public CommandBus(
					string name,
					bool watchSlowMsg = true,
					TimeSpan? slowMsgThreshold = null,
					TimeSpan? slowCmdThreshold = null)
			: base(name, watchSlowMsg, slowMsgThreshold)
		{
			var slowMsgThreshold1 = slowMsgThreshold ?? TimeSpan.FromMilliseconds(100);
			var slowCmdThreshold1 = slowCmdThreshold ?? TimeSpan.FromMilliseconds(500);
			_publisher = new CommandPublisher(this, slowMsgThreshold1, slowCmdThreshold1);
			_handleWrappers = new Dictionary<Type, object>();
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="command"></param>
		/// <param name="exceptionMsg"></param>
		/// <param name="responseTimeout"></param>
		/// <param name="ackTimeout"></param>
		/// <returns></returns>
		public void Fire(
						Command command,
						string exceptionMsg = null,
						TimeSpan? responseTimeout = null,
						TimeSpan? ackTimeout = null)
		{
			_publisher.Fire(command, exceptionMsg, responseTimeout, ackTimeout);
		}




		public bool TryFire(Command command,
						out CommandResponse response,
						TimeSpan? responseTimeout = null,
						TimeSpan? ackTimeout = null)
		{
			return _publisher.TryFire(command, out response, responseTimeout, ackTimeout);
		}

		public bool TryFire(Command command, TimeSpan? responseTimeout = null, TimeSpan? ackTimeout = null)
		{
			return _publisher.TryFire(command, out var _, responseTimeout, ackTimeout);
		}

		public IDisposable Subscribe<T>(IHandleCommand<T> handler) where T : Command
		{
			if (HasSubscriberFor<T>())
				throw new ExistingHandlerException("Duplicate registration for command type.");
			var handleWrapper = new CommandHandler<T>(this, handler);
			_handleWrappers.Add(typeof(T), handleWrapper);
			Subscribe(handleWrapper);
			return new SubscriptionDisposer(() => { Unsubscribe(handler); return Unit.Default; });
		}
		public void Unsubscribe<T>(IHandleCommand<T> handler) where T : Command
		{
			if (!_handleWrappers.TryGetValue(typeof(T), out var wrapper)) return;
			Unsubscribe((CommandHandler<T>)wrapper);
			_handleWrappers.Remove(typeof(T));
		}
	}


	[Serializable]
	public class ExistingHandlerException : Exception
	{

		public ExistingHandlerException()
		{
		}

		public ExistingHandlerException(string message) : base(message)
		{
		}

		public ExistingHandlerException(string message, Exception inner) : base(message, inner)
		{
		}

		protected ExistingHandlerException(
			SerializationInfo info,
			StreamingContext context) : base(info, context)
		{
		}
	}

}
