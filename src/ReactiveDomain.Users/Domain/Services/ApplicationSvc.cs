using System;
using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Users.Domain.Aggregates;
using ReactiveDomain.Users.Messages;
using ReactiveDomain.Users.ReadModels;

namespace ReactiveDomain.Users.Domain.Services
{
    /// <summary>
    /// The service that fronts the Application aggregate.
    /// </summary>
    public class ApplicationSvc :
        TransientSubscriber,
        IHandleCommand<ApplicationMsgs.CreateApplication>,
        //todo: handle other application commands
        //IHandleCommand<ApplicationMsgs.ConfigureApplication>
        IHandleCommand<RoleMsgs.CreateRole>
        //todo: handle other role commands
        //IHandleCommand<RoleMsgs.RemoveRole>
        //IHandleCommand<RoleMsgs.RoleMigrated>
    {
        
        private readonly CorrelatedStreamStoreRepository _repo;
        private readonly ApplicationsRM _applicationsRm;

        /// <summary>
        /// Create a service to act on Application aggregates.
        /// </summary>
        /// <param name="repo">The repository for interacting with the EventStore.</param>
        /// <param name="getListener">Function for getting a Listener from the EventStore Repo.</param>
        /// <param name="bus">The dispatcher.</param>
        public ApplicationSvc(
            IRepository repo,
            Func<IListener> getListener,
            IDispatcher bus)
            : base(bus)
        {
            _repo = new CorrelatedStreamStoreRepository(repo);
            _applicationsRm = new ApplicationsRM(getListener);

            Subscribe<ApplicationMsgs.CreateApplication>(this);
            Subscribe<RoleMsgs.CreateRole>(this);
            
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
        public CommandResponse Handle(ApplicationMsgs.CreateApplication command)
        {
            var application = new Application(
                command.Id,
                command.Name,
                command.Version);
            _repo.Save(application); //n.b. this will throw on duplicate application due to optimistic stream concurrency checks in the event store
            return command.Succeed();
        }
        /// <summary>
        /// Given the create role command, creates a role created event.
        /// </summary>
        public CommandResponse Handle(RoleMsgs.CreateRole cmd) {

            var application = _repo.GetById<Application>(cmd.ApplicationId,cmd);
            application.AddRole(cmd.RoleId, cmd.Name);
            _repo.Save(application);
            return cmd.Succeed();
        }
    }
}
