using System;
using Elbe.Messages;
using NLog;
using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;

namespace Elbe.Domain
{
    /// <summary>
    /// The service that fronts the Application aggregate.
    /// </summary>
    public class ApplicationSvc :
        TransientSubscriber,
        IHandleCommand<ApplicationMsgs.RegisterApplication>
    {
        private static readonly ILogger Log = LogManager.GetLogger(Bootstrap.LogName);
        
        private readonly CorrelatedStreamStoreRepository _repo;
        private readonly ApplicationsRM _applicationsRm;

        /// <summary>
        /// Create a service to act on Application aggregates.
        /// </summary>
        /// <param name="repo">The repository for interacting with the EventStore.</param>
        /// <param name="bus">The dispatcher.</param>
        public ApplicationSvc(
            IRepository repo,
            IDispatcher bus)
            : base(bus)
        {
            _repo = new CorrelatedStreamStoreRepository(repo);
            _applicationsRm = new ApplicationsRM();

            Subscribe<ApplicationMsgs.RegisterApplication>(this);
            
        }

        private bool _disposed;

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (_disposed) return;
            if (disposing)
                _applicationsRm.Dispose();
            _disposed = true;
        }

        /// <summary>
        /// Handle a ApplicationMsgs.RegisterApplication command.
        /// </summary>
        /// <exception cref="DuplicateApplicationException"></exception>
        public CommandResponse Handle(ApplicationMsgs.RegisterApplication command)
        {
            if (_repo.TryGetById<Application>(command.Id, out _, command)
                || _applicationsRm.ApplicationExists(command.Name))
            {
                throw new DuplicateApplicationException(command.Name);
            }
            var application = new Application(
                command.Id,
                command.Name,
                command.OneRolePerUser,
                command.Roles,
                command.SecAdminRole,
                command.DefaultUser,
                command.DefaultDomain,
                command.DefaultUserRoles,
                command);
            _repo.Save(application);
            return command.Succeed();
        }
    }
}
