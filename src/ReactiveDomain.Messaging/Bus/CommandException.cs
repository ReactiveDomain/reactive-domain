using System;
using System.Runtime.Serialization;

namespace ReactiveDomain.Messaging.Bus {
	
    [Serializable]
    public class CommandException : Exception
    {
        public readonly Command Command;

        public CommandException(Command command) : base($"{command?.GetType().Name}: Failed")
        {
            Command = command;
        }

        public CommandException(string message, Command command) : base($"{command?.GetType().Name}: {message}")
        {
            Command = command;
        }

        public CommandException(string message, Exception inner, Command command) : base($"{command?.GetType().Name}: {message}", inner)
        {
            Command = command;
        }

        protected CommandException(
            SerializationInfo info,
            StreamingContext context) : base(info, context)
        {
        }
    }

	[Serializable]
    public class CommandCanceledException : CommandException
    {

        public CommandCanceledException(Command command) : base(" canceled", command)
        {
        }

        public CommandCanceledException(string message, Command command) : base(message, command)
        {
        }

        public CommandCanceledException(string message, Exception inner, Command command) : base(message, inner, command)
        {
        }

        protected CommandCanceledException(
            SerializationInfo info,
            StreamingContext context) : base(info, context)
        {
        }
    }
    [Serializable]
    public class CommandTimedOutException : CommandException
    {

        public CommandTimedOutException(Command command) : base(" timed out", command)
        {
        }

        public CommandTimedOutException(string message, Command command) : base(message, command)
        {
        }

        public CommandTimedOutException(string message, Exception inner, Command command) : base(message, inner, command)
        {
        }

        protected CommandTimedOutException(
            SerializationInfo info,
            StreamingContext context) : base(info, context)
        {
        }
    }
    /// <summary>
    /// More than one handler acked this message.
    /// Most of this means the command is subscribed on
    /// multiple connected buses
    /// </summary>
    [Serializable]
    public class CommandOversubscribedException : CommandException
    {

        public CommandOversubscribedException(Command command) : base(" oversubscribed", command)
        {
        }

        public CommandOversubscribedException(string message, Command command) : base(message, command)
        {
        }

        public CommandOversubscribedException(string message, Exception inner, Command command) : base(message, inner, command)
        {
        }

        protected CommandOversubscribedException(
            SerializationInfo info,
            StreamingContext context) : base(info, context)
        {
        }
    }
	[Serializable]
	public class CommandNotHandledException : CommandException
	{

		public CommandNotHandledException(Command command) : base(" not handled", command)
		{
		}

		public CommandNotHandledException(string message, Command command) : base(message, command)
		{
		}

		public CommandNotHandledException(string message, Exception inner, Command command) : base(message, inner, command)
		{
		}

		protected CommandNotHandledException(
			SerializationInfo info,
			StreamingContext context) : base(info, context)
		{
		}
	}
}