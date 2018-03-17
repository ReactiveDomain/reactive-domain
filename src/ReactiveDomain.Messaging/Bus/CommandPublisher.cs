using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReactiveDomain.Messaging.Bus
{
	public class CommandPublisher : ICommandPublisher
	{
		private readonly IBus _bus;
		private readonly CommandManager _manager;
		private readonly TimeSpan? _slowMsgThreshold;
		private readonly TimeSpan? _slowCmdThreshold;

		public CommandPublisher(IBus bus, TimeSpan? slowMsgThreshold, TimeSpan? slowCmdThreshold)
		{
			_bus = bus;
			_slowMsgThreshold = slowMsgThreshold;
			_slowCmdThreshold = slowCmdThreshold;
			_manager = new CommandManager(bus);
		}
		public void Fire(Command command, string exceptionMsg = null, TimeSpan? responseTimeout = null, TimeSpan? ackTimeout = null)
		{
			var tCmd = command as TokenCancellableCommand;
			if (tCmd?.IsCanceled ?? false)
			{
				_bus.Publish(tCmd.Canceled());
				return;
			}

			var rslt = Execute(command, responseTimeout, ackTimeout);
			if (rslt is Success) return;

			var fail = rslt as Fail;
			if (fail?.Exception != null)
				throw new CommandException(exceptionMsg ?? fail.Exception.Message, fail.Exception, command);
			else
				throw new CommandException(exceptionMsg ?? $"{command.GetType().Name}: Failed", command);
		}
		public bool TryFire(Command command,
			out CommandResponse response,
			TimeSpan? responseTimeout = null,
			TimeSpan? ackTimeout = null)
		{
			try
			{
				//todo: we're not chaining through the fire method here because it doesn't give 
				//us the command response to return so there are some duplicated checks 
				var tCmd = command as TokenCancellableCommand;
				if (tCmd?.IsCanceled ?? false)
				{
					response = tCmd.Canceled();
					_bus.Publish(response);
					return false;
				}
				response = Execute(command, responseTimeout, ackTimeout);
			}
			catch (Exception ex)
			{
				response = command.Fail(ex);
			}
			return response is Success;
		}

		public bool TryFire(Command command, TimeSpan? responseTimeout = null, TimeSpan? ackTimeout = null)
		{
			return TryFire(command, out var _, responseTimeout, ackTimeout);

		}

		private CommandResponse Execute(
			Command command,
			TimeSpan? responseTimeout = null,
			TimeSpan? ackTimeout = null)
		{
			TaskCompletionSource<CommandResponse> tcs = null;
			try
			{
				CommandReceived(command, command.GetType(), "");
				tcs = _manager.RegisterCommandAsync(
					command,
					ackTimeout ?? _slowMsgThreshold,
					responseTimeout ?? _slowCmdThreshold);
			}
			catch (CommandException ex)
			{
				tcs?.SetResult(command.Fail(ex));
				throw;
			}
			catch (Exception ex)
			{
				tcs?.SetResult(command.Fail(ex));
				throw new CommandException("Error executing command: ", ex, command);
			}
			//start new task/thread to publish command
			Task.Run(() =>
			{
				try
				{
					//n.b. if this does not throw result will be set asynchronously 
					//in the registered handler in the _manager 
					_bus.Publish(command);
				}
				catch (Exception ex)
				{
					tcs.SetResult(command.Fail(ex));
					throw;
				}
			});
			try
			{
				//blocking caller until result is set 
				return tcs.Task.Result;
			}
			catch (AggregateException aggEx)
			{
				if (aggEx.InnerException != null)
				{
					throw aggEx.InnerException;
				}
				throw;
			}
		}


		public virtual void NoCommandHandler(dynamic cmd, Type type)
		{
			//replace with message published on ack timeout
			//We can't know this here
		}

		public virtual void PostHandleCommand(dynamic cmd, Type type, string handlerName, dynamic response,
			TimeSpan handleTimeSpan)
		{
			//replace with message published from Command handler
			//We can't know this here
		}

		public virtual void CommandReceived(dynamic cmd, Type type, string firedBy)
		{
			//replace with message published from Command handler
			//We can't know this here
		}
	}
}
