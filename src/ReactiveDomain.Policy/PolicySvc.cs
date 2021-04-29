using System;
using System.Linq;
using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Users.Domain.Aggregates;
using ReactiveDomain.Users.Messages;

namespace ReactiveDomain.Users.Domain.Services
{
    public class PolicySvc :
            TransientSubscriber,
            IHandleCommand<RoleMsgs.CreateRole>
    //todo: handle other role commands
    //IHandleCommand<RoleMsgs.RoleMigrated>
    {
        private readonly Guid _applicationId;
        private readonly ICorrelatedRepository _repo;

        public PolicySvc(
            Guid applicationId,
            IConfiguredConnection conn,
            ICommandSubscriber cmdSource)
            : base(cmdSource)
        {
            _applicationId = applicationId;
            _repo = conn.GetCorrelatedRepository(caching: true);

            Subscribe<RoleMsgs.CreateRole>(this);
        }
        /// <summary>
        /// Given the create role command, creates a role created event.
        /// </summary>
        public CommandResponse Handle(RoleMsgs.CreateRole cmd)
        {

            var application = _repo.GetById<SecuredApplicationAgg>(_applicationId, cmd);
            application.Policies.First(p=> p.Id == cmd.PolicyId).AddRole(cmd.RoleId, cmd.Name);
            _repo.Save(application);
            return cmd.Succeed();
        }
    }
}
