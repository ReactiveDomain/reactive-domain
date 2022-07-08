using ReactiveDomain.Foundation;
using ReactiveDomain.IdentityStorage.Domain;
using ReactiveDomain.IdentityStorage.Messages;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;

namespace ReactiveDomain.IdentityStorage.Services
{
    public class ClientSvc :
        TransientSubscriber,
        IHandleCommand<ClientMsgs.CreateClient>,
        IHandleCommand<ClientMsgs.AddClientSecret>,
        IHandleCommand<ClientMsgs.RemoveClientSecret>
    {
        private readonly CorrelatedStreamStoreRepository _repo;

        public ClientSvc(IRepository repo, IDispatcher bus) : base(bus)
        {
            _repo = new CorrelatedStreamStoreRepository(repo);
            Subscribe<ClientMsgs.CreateClient>(this);
            Subscribe<ClientMsgs.AddClientSecret>(this);
            Subscribe<ClientMsgs.RemoveClientSecret>(this);
        }

        public CommandResponse Handle(ClientMsgs.CreateClient command)
        {
            var client = new Client(
                                command.ClientId,
                                command.ApplicationId,
                                command.ClientName,
                                command.EncryptedClientSecret,
                                command.RedirectUris,
                                command.PostLogoutRedirectUris,
                                command.FrontChannelLogoutUri,
                                command);
            try
            {
                _repo.Save(client);
            }
            catch (WrongExpectedVersionException)
            {
                throw new DuplicateClientException(command.ClientId, command.ClientName);
            }
            return command.Succeed();
        }

        public CommandResponse Handle(ClientMsgs.AddClientSecret command)
        {
            var client = _repo.GetById<Client>(command.ClientId, command);
            client.AddClientSecret(command.EncryptedClientSecret);
            _repo.Save(client);
            return command.Succeed();
        }

        public CommandResponse Handle(ClientMsgs.RemoveClientSecret command)
        {
            var client = _repo.GetById<Client>(command.ClientId, command);
            client.RemoveClientSecret(command.EncryptedClientSecret);
            _repo.Save(client);
            return command.Succeed();
        }
    }
}
