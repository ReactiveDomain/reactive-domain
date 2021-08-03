using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Policy.Messages;

namespace ReactiveDomain.Policy
{
    public class ApplicationSvc:
        TransientSubscriber,
        IHandleCommand<ApplicationMsgs.CreateApplication> {
        private readonly IConfiguredConnection _conn;
        private readonly IRepository _repo;

        public ApplicationSvc(
            IConfiguredConnection conn,
            ICommandSubscriber subscriber)
            : base(subscriber) {
            _conn = conn;
            _repo = conn.GetRepository();
            Subscribe<ApplicationMsgs.CreateApplication>(this);
        }

        public CommandResponse Handle(ApplicationMsgs.CreateApplication cmd) {
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
