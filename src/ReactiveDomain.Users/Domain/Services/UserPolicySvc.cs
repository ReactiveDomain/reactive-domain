using System;
using System.Collections.Generic;
using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Users.Domain.Aggregates;
using ReactiveDomain.Users.Messages;

namespace ReactiveDomain.Users.Domain.Services
{
    /// <summary>
    /// Service to cofigure user policy
    /// </summary>
    public sealed class UserPolicySvc :
        IDisposable,
        IHandleCommand<UserPolicyMsgs.AddPolicy>,
        IHandleCommand<UserPolicyMsgs.RemovePolicy>,
        IHandleCommand<UserPolicyMsgs.AddRole>,
        IHandleCommand<UserPolicyMsgs.RemoveRole>

    {

        private readonly CorrelatedStreamStoreRepository _repo;
        List<IDisposable> _subscriptions = new List<IDisposable>();

        /// <summary>
        /// Create a service to act on User aggregates.
        /// </summary>
        /// <param name="repo">The repository for interacting with the EventStore.</param>
        /// <param name="bus">The dispatcher.</param>
        public UserPolicySvc(
            ICommandSubscriber bus,
            IRepository repo)
        {
            _repo = new CorrelatedStreamStoreRepository(repo);
            _subscriptions.Add(bus.Subscribe<UserPolicyMsgs.AddPolicy>(this));
            _subscriptions.Add(bus.Subscribe<UserPolicyMsgs.RemovePolicy>(this));
            _subscriptions.Add(bus.Subscribe<UserPolicyMsgs.AddRole>(this));
            _subscriptions.Add(bus.Subscribe<UserPolicyMsgs.RemoveRole>(this));
        }



        public CommandResponse Handle(UserPolicyMsgs.AddPolicy command)
        {
            var user = _repo.GetById<UserAgg>(command.UserId, command);
            throw new NotImplementedException();
        }
        public CommandResponse Handle(UserPolicyMsgs.RemovePolicy command)
        {
            throw new NotImplementedException();
        }
        public CommandResponse Handle(UserPolicyMsgs.AddRole command)
        {
            throw new NotImplementedException();
        }
        public CommandResponse Handle(UserPolicyMsgs.RemoveRole command)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            foreach (var sub in _subscriptions)
            {
                sub?.Dispose();
            }
        }


    }
}
