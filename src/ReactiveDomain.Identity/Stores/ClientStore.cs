using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IdentityModel;
using IdentityServer4.Models;
using IdentityServer4.Stores;
using ReactiveDomain.Foundation;
using ReactiveDomain.Identity.Messages;
using ReactiveDomain.Messaging.Bus;
namespace ReactiveDomain.Identity.Stores
{
    public class ClientStore :
        ReadModelBase,
        IClientStore,
        IHandle<ClientMsgs.ClientCreated>,
        IHandle<ClientMsgs.ClientSecretAdded>,
        IHandle<ClientMsgs.ClientSecretRemoved>
    {

        public ClientStore(IConfiguredConnection conn) : base(nameof(ClientStore), () => conn.GetListener(nameof(ClientStore)))
        {
            long checkpoint;

            EventStream.Subscribe<ClientMsgs.ClientCreated>(this);
            EventStream.Subscribe<ClientMsgs.ClientSecretAdded>(this);
            EventStream.Subscribe<ClientMsgs.ClientSecretRemoved>(this);

            using (var reader = conn.GetReader(nameof(ClientStore),Handle))
            {             
                reader.Read<Domain.Client>(()=> Idle);
                checkpoint = reader.Position ?? StreamPosition.Start;
            }
            Start<Domain.Client>(checkpoint);
        }

        private readonly Dictionary<Guid, Client> _clientsByAppId = new Dictionary<Guid, Client>();
        private readonly Dictionary<Guid, Client> _clientsByClientId = new Dictionary<Guid, Client>();
        private readonly Dictionary<string, Client> _clientsByClientName = new Dictionary<string, Client>();
        public async Task<Client> FindClientByIdAsync(string clientId)
        {
            Client result = null;
            if (Guid.TryParse(clientId, out Guid id)) {
                _clientsByClientId.TryGetValue(id, out  result);
            }
            return await Task.FromResult(result);
        }
            public async Task<Client> FindClientByIdAsync(Guid clientId)
        {
            _clientsByClientId.TryGetValue(clientId, out var client);
            return await Task.FromResult(client);
        }
        public async Task<Client> FindClientByNameAsync(string clientName)
        {
            _clientsByClientName.TryGetValue(clientName, out var client);
            return await Task.FromResult(client);
        }
        public IReadOnlyList<Client> Clients => _clientsByClientId.Values.ToList().AsReadOnly();
        public void Handle(ClientMsgs.ClientCreated @event) {
            var client =
                 new Client
                 {
                     ClientId = @event.ClientId.ToString("N"),
                     ClientName = @event.ClientName,
                     AllowedGrantTypes = @event.GrantTypes,
                     AllowedScopes = @event.AllowedScopes,
                     RedirectUris =  @event.RedirectUris,
                     PostLogoutRedirectUris =  @event.PostLogoutRedirectUris,
                     FrontChannelLogoutUri =  @event.FrontChannelLogoutUri,
                     AllowOfflineAccess = true,
                     RequireConsent = false,
                     AlwaysIncludeUserClaimsInIdToken = true
                 };
            _clientsByAppId.Add(@event.ApplicationId, client);
            _clientsByClientId.Add(@event.ClientId, client);
        }

        public void Handle(ClientMsgs.ClientSecretAdded @event)
        {
            if (_clientsByClientId.TryGetValue(@event.ClientId, out var client))
            {
                client.ClientSecrets.Add(new Secret(@event.EncryptedClientSecret.ToSha256()));
            }

        }

        public void Handle(ClientMsgs.ClientSecretRemoved @event)
        {
            if (_clientsByClientId.TryGetValue(@event.ClientId, out var client))
            {
                client.ClientSecrets.Remove(new Secret(@event.EncryptedClientSecret.ToSha256()));
            }
        }
    }
}