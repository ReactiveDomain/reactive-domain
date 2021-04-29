using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;

namespace ReactiveDomain.Users.Identity
{

    public class ClientApplicationSvc :
        IHandleCommand<ClientApplicationMsgs.CreateClientApplication>,
        IHandleCommand<ClientApplicationMsgs.DefineAccessRole>,
        IHandleCommand<ClientApplicationMsgs.ChangeClientSecret>
    {
        private readonly IRepository _repo;
        public ClientApplicationSvc(IRepository repo)
        {
            _repo = repo;
        }

        public CommandResponse Handle(ClientApplicationMsgs.CreateClientApplication command)
        {
            var application = new ClientApplication(
                command.ApplicationId,
                command.ApplicationDisplayName,
                command.ClientId,
                command.ClientSecret);
            _repo.Save(application);
            return command.Succeed();
        }
        public CommandResponse Handle(ClientApplicationMsgs.DefineAccessRole command)
        {
            var application = _repo.GetById<ClientApplication>(command.ClientApplicationId);
            application.SetAccessRole(command.RoleName);
            _repo.Save(application);
            return command.Succeed();
        }
        public CommandResponse Handle(ClientApplicationMsgs.ChangeClientSecret command)
        {
            var application = _repo.GetById<ClientApplication>(command.ClientApplicationId);
            application.ChangeClientSecret(command.ClientSecret);
            _repo.Save(application);
            return command.Succeed();
        }
    }
}
