using System;
using System.Collections.Generic;
using System.Text;
using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Policy.Application;
using ReactiveDomain.Policy.Messages;

namespace ReactiveDomain.Policy
{
    public class ApplicationSvc:
        IHandleCommand<ApplicationMsgs.CreateApplication> {
        private readonly IConfiguredConnection _conn;
        private readonly IRepository _repo;

        public ApplicationSvc(IConfiguredConnection conn) {
            _conn = conn;
            _repo = conn.GetRepository();
        }

        public CommandResponse Handle(ApplicationMsgs.CreateApplication cmd) {
            var app = new Domain.SecuredApplication(cmd.ApplicationId,cmd.Name,cmd.Version,cmd.OneRolePerUser, cmd);
            _repo.Save(app);
            return cmd.Succeed();
        }
    }
}
