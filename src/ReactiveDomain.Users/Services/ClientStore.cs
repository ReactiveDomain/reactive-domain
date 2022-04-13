using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using IdentityModel;
using IdentityServer4.Models;
using IdentityServer4.Stores;
using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging.Bus;
using RD = ReactiveDomain.Users.Domain;
using ReactiveDomain.Users.Messages;

namespace ReactiveDomain.Users.Services
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

            using (var reader = conn.GetReader(nameof(ClientStore), Handle))
            {
                reader.Read<RD.Client>(() => Idle);
                checkpoint = reader.Position ?? StreamPosition.Start;
            }
            Start<RD.Client>(checkpoint);
        }

        private readonly Dictionary<string, Client> _clientsByClientName = new Dictionary<string, Client>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<Guid, string> _clientNameById = new Dictionary<Guid, string>();
        public async Task<Client> FindClientByIdAsync(string clientId)
        {
            var clientName = string.Empty;
            if (Guid.TryParse(clientId, out Guid parsed) && _clientNameById.ContainsKey(parsed))
            {
                clientName = _clientNameById[parsed];
            }
            _clientsByClientName.TryGetValue(clientName, out var client);
            return await Task.FromResult(client);
        }

        public Client FindClientByName(string clientName)
        {  
            _clientsByClientName.TryGetValue(clientName, out var client);
            return client;
        }
        public IReadOnlyList<Client> Clients => _clientsByClientName.Values.ToList().AsReadOnly();
        public void Handle(ClientMsgs.ClientCreated @event)
        {
            var client =
                 new Client
                 {
                     ClientId = @event.ClientId.ToString("N"),
                     ClientName = @event.ClientName,
                     AllowedGrantTypes = @event.GrantTypes,
                     AllowedScopes = @event.AllowedScopes,
                     RedirectUris = @event.RedirectUris,
                     PostLogoutRedirectUris = @event.PostLogoutRedirectUris,
                     FrontChannelLogoutUri = @event.FrontChannelLogoutUri,
                     AllowOfflineAccess = true,
                     RequireConsent = false,
                     AlwaysIncludeUserClaimsInIdToken = true,
                     Enabled = true
                 };
            _clientsByClientName.Add(@event.ClientName, client);
            _clientNameById.Add(@event.ClientId, @event.ClientName);
        }

        public void Handle(ClientMsgs.ClientSecretAdded @event)
        {
            if (_clientNameById.TryGetValue(@event.ClientId, out var name))
            {
                _clientsByClientName[name].ClientSecrets.Add(new Secret(@event.EncryptedClientSecret.ToSha256()));
            }

        }

        public void Handle(ClientMsgs.ClientSecretRemoved @event)
        {
            if (_clientNameById.TryGetValue(@event.ClientId, out var name))
            {
                _clientsByClientName[name].ClientSecrets.Remove(new Secret(@event.EncryptedClientSecret.ToSha256()));
            }
        }
    }
}