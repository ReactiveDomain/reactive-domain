using System;

namespace ReactiveDomain.Messaging
{
    public interface ICommandResponse : IMessage
    {
        Guid CommandId { get; }
        Type CommandType { get; }
        ICommand SourceCommand { get; }
    }
}