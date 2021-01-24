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
        IHandleCommand<ApplicationMsgs.RetireApplication>,
        IHandleCommand<ApplicationMsgs.UnretireApplication>,
        IHandleCommand<RoleMsgs.CreateRole>,
        IHandleCommand<RoleMsgs.AssignChildRole>,
        IHandleCommand<RoleMsgs.AddPermission>,
        IHandleCommand<RoleMsgs.AssignPermission>
        //todo: handle other role commands
        //IHandleCommand<RoleMsgs.RoleMigrated>
    {
        
        private readonly ICorrelatedRepository _repo;
        private readonly ApplicationsRM _applicationsRm;

        /// <summary>
        /// Create a service to act on Application aggregates.
        /// </summary>
        /// <param name="conn">A configured connection for interacting with the EventStore.</param>
        /// <param name="cmdSource">The dispatcher to subscribe to.</param>
        public ApplicationSvc(
            IConfiguredConnection conn,
            ICommandSubscriber cmdSource)
            : base(cmdSource) {
            _repo = conn.GetCorrelatedRepository(caching:true);
            _applicationsRm = new ApplicationsRM(conn);

            Subscribe<ApplicationMsgs.CreateApplication>(this);
            Subscribe<ApplicationMsgs.RetireApplication>(this);
            Subscribe<ApplicationMsgs.UnretireApplication>(this);
            Subscribe<RoleMsgs.CreateRole>(this);
            Subscribe<RoleMsgs.AssignChildRole>(this);
            Subscribe<RoleMsgs.AddPermission>(this);
            Subscribe<RoleMsgs.AssignPermission>(this);
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
            var application = new ApplicationRoot(
                command.Id,
                command.Name,
                command.Version,
                command);
            
            _repo.Save(application); //n.b. this will throw on duplicate application due to optimistic stream concurrency checks in the event store
            return command.Succeed();
        }

        public CommandResponse Handle(ApplicationMsgs.RetireApplication command)
        {
            var application = _repo.GetById<ApplicationRoot>(command.Id, command);
            application.Retire();
            _repo.Save(application);
            return command.Succeed();
        }

        public CommandResponse Handle(ApplicationMsgs.UnretireApplication command)
        {
            var application = _repo.GetById<ApplicationRoot>(command.Id, command);
            application.Unretire();
            _repo.Save(application);
            return command.Succeed();
        }

        /// <summary>
        /// Given the create role command, creates a role created event.
        /// </summary>
        public CommandResponse Handle(RoleMsgs.CreateRole cmd) {

            var application = _repo.GetById<ApplicationRoot>(cmd.ApplicationId,cmd);
            application.AddRole(cmd.RoleId, cmd.Name);
            _repo.Save(application);
            return cmd.Succeed();
        }

        public CommandResponse Handle(RoleMsgs.AssignChildRole cmd) {
            var application = _repo.GetById<ApplicationRoot>(cmd.ApplicationId,cmd);
            application.AssignChildRole(cmd.ParentRoleId, cmd.ChildRoleId);
            _repo.Save(application);
            return cmd.Succeed();
        }

        public CommandResponse Handle(RoleMsgs.AddPermission cmd) {
            var application = _repo.GetById<ApplicationRoot>(cmd.ApplicationId,cmd);
            application.AddPermission(cmd.PermissionId, cmd.PermissionName);
            _repo.Save(application);
            return cmd.Succeed();
        }

        public CommandResponse Handle(RoleMsgs.AssignPermission cmd) {
            var application = _repo.GetById<ApplicationRoot>(cmd.ApplicationId,cmd);
            application.AssignPermission(cmd.RoleId, cmd.PermissionId);
            _repo.Save(application);
            return cmd.Succeed();
        }
    }
}
