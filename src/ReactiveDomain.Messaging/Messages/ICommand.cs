using System;
using System.Threading;

namespace ReactiveDomain.Messaging
{
    public interface ICommand : ICorrelatedMessage
    {
        bool IsCancelable { get; }
        bool IsCanceled { get; }
        CancellationToken? CancellationToken { get; }

        void RegisterOnCancellation(Action action);
        CommandResponse Succeed();
        CommandResponse Fail(Exception ex = null);
        CommandResponse Canceled();
    }
}