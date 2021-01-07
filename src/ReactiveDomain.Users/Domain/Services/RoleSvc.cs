using System;
using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Users.Domain.Aggregates;
using ReactiveDomain.Users.Messages;

namespace ReactiveDomain.Users.Domain.Services
{
    /// <summary>
    /// The service that fronts the role aggregate.
    /// </summary>
    public class RoleSvc :
        TransientSubscriber,
        IHandleCommand<RoleMsgs.CreateRole>
    {
        private readonly CorrelatedStreamStoreRepository _repo;
        private readonly RolesRM _rolesRM;

        /// <summary>
        /// Create a service to act on Role aggregates.
        /// </summary>
        public RoleSvc(
            Func<IListener> getListener,
            IRepository repo,
            IDispatcher bus)
            : base(bus)
        {
            _repo = new CorrelatedStreamStoreRepository(repo);
            _rolesRM = new RolesRM(getListener);

            // ReSharper disable once RedundantTypeArgumentsOfMethod
            Subscribe<RoleMsgs.CreateRole>(this);
        }

        private bool _disposed;

        /// <summary>
        /// Dispose of the roles read model.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (_disposed) return;
            if (disposing)
                _rolesRM.Dispose();
            _disposed = true;
        }

        /// <summary>
        /// Given the create role command, creates a role created event.
        /// </summary>
        public CommandResponse Handle(RoleMsgs.CreateRole command)
        {
            if (!_repo.TryGetById<Role>(
                        command.RoleId, 
                        out var role, 
                        command) 
                && !_rolesRM.RoleExists(command.Name, command.Application))
            {
                role = new Role(
                                command.RoleId, 
                                command.Name, 
                                command.Application, 
                                command);
                _repo.Save(role);
            }
            else
            {
                throw new DuplicateRoleException(command.Name, command.Application);
            }
            return command.Succeed();
        }
    }
}
