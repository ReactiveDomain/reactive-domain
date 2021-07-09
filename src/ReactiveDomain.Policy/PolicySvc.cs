using System;
using System.Linq;
using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Policy.Domain;
using ReactiveDomain.Policy.Messages;
using ReactiveDomain.Users.Domain;
using ReactiveDomain.Util;

namespace ReactiveDomain.Policy
{
    public class PolicySvc :
            TransientSubscriber,
            IHandleCommand<RoleMsgs.CreateRole>,
            IHandleCommand<PolicyUserMsgs.AddPolicyUser>,
            IHandleCommand<PolicyUserMsgs.AddRole>,
            IHandleCommand<PolicyUserMsgs.RemoveRole>,
            IHandleCommand<PolicyUserMsgs.DeactivateUser>,
            IHandleCommand<PolicyUserMsgs.ReactivateUser>
    {
      
        private readonly ICorrelatedRepository _repo;

        public PolicySvc(
            IConfiguredConnection conn,
            ICommandSubscriber cmdSource)
            : base(cmdSource)
        {
            _repo = conn.GetCorrelatedRepository(caching: true);

            Subscribe<RoleMsgs.CreateRole>(this);
            Subscribe<PolicyUserMsgs.AddPolicyUser>(this);
            Subscribe<PolicyUserMsgs.AddRole>(this);
            Subscribe<PolicyUserMsgs.RemoveRole>(this);
            Subscribe<PolicyUserMsgs.DeactivateUser>(this);
            Subscribe<PolicyUserMsgs.ReactivateUser>(this);
        }
        /// <summary>
        /// Given the create role command, creates a role created event.
        /// </summary>
        public CommandResponse Handle(RoleMsgs.CreateRole cmd)
        {
            var application = _repo.GetById<SecuredApplication>(cmd.AppId, cmd);
            application.Policies.First(p => p.Id == cmd.PolicyId).AddRole(cmd.RoleId ?? Guid.NewGuid(), cmd.Name);
            _repo.Save(application);
            return cmd.Succeed();
        }

        public CommandResponse Handle(PolicyUserMsgs.AddPolicyUser cmd)
        {
            var policy = _repo.GetById<Domain.SecuredApplication>(cmd.ApplicationId, cmd).DefaultPolicy;
            if (policy.Id != cmd.PolicyId) { throw new NotSupportedException("Multiple Policies per Application is not supported. (or bad policy id)"); }

            var policyUser = new Domain.PolicyUser(cmd.PolicyUserId, policy.Id, cmd.UserId, cmd);
            _repo.Save(policyUser);
            return cmd.Succeed();
        }

        public CommandResponse Handle(PolicyUserMsgs.AddRole cmd)
        {
            var user = _repo.GetById<Domain.PolicyUser>(cmd.PolicyUserId, cmd);
            user.AddRole(cmd.RoleName, cmd.RoleId);
            _repo.Save(user);
            return cmd.Succeed();
        }

        public CommandResponse Handle(PolicyUserMsgs.RemoveRole cmd)
        {
            var user = _repo.GetById<Domain.PolicyUser>(cmd.PolicyUserId, cmd);
            user.RemoveRole(cmd.RoleName, cmd.RoleId);
            _repo.Save(user);
            return cmd.Succeed();
        }

        public CommandResponse Handle(PolicyUserMsgs.DeactivateUser cmd)
        {
            var user = _repo.GetById<Domain.PolicyUser>(cmd.PolicyUserId, cmd);
            user.Deactivate();
            _repo.Save(user);
            return cmd.Succeed();
        }

        public CommandResponse Handle(PolicyUserMsgs.ReactivateUser cmd)
        {
            var user = _repo.GetById<Domain.PolicyUser>(cmd.PolicyUserId, cmd);
            user.Reactivate();
            _repo.Save(user);
            return cmd.Succeed();
        }
    }
}
