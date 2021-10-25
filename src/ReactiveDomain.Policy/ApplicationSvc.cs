using System;
using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Policy.Messages;
using ReactiveDomain.Policy.ReadModels;

namespace ReactiveDomain.Policy
{
    public class ApplicationSvc:
        TransientSubscriber,
        IHandleCommand<ApplicationMsgs.CreateApplication> {
        private readonly IConfiguredConnection _conn;
        private readonly ICorrelatedRepository _repo;
        private readonly FilteredPoliciesRM _rm;

        public ApplicationSvc(
            IConfiguredConnection conn,
            ICommandSubscriber subscriber)
            : base(subscriber) {
            _conn = conn;
            _repo = conn.GetCorrelatedRepository();
            _rm = new FilteredPoliciesRM(conn);
            Subscribe<ApplicationMsgs.CreateApplication>(this);
        }

        public CommandResponse Handle(ApplicationMsgs.CreateApplication cmd)
        {
            if (_rm.ApplicationExists(cmd.Name, new Version(cmd.SecurityModelVersion)))
                throw new DuplicateApplicationException(cmd.Name, cmd.SecurityModelVersion);
            var app = new Domain.SecuredApplication(
                            cmd.ApplicationId,
                            cmd.DefaultPolicyId,
                            cmd.Name,
                            cmd.SecurityModelVersion,
                            cmd.OneRolePerUser,
                            cmd);
            _repo.Save(app);
            return cmd.Succeed();
        }
    }
}
