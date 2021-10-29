using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using IdentityModel;
using IdentityServer4.Models;
using IdentityServer4.Stores;
using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Policy.Domain;
using ReactiveDomain.Policy.Messages;

namespace ReactiveDomain.Identity.Stores
{
    public class ClientStore :
        ReadModelBase,
        IClientStore,
        IHandle<ApplicationMsgs.STSClientDetailsAdded>,
        IHandle<ApplicationMsgs.STSClientSecretAdded>,
        IHandle<ApplicationMsgs.STSClientSecretRemoved>
    {

        public ClientStore(IConfiguredConnection conn) : base(nameof(ClientStore), () => conn.GetListener(nameof(ClientStore)))
        {
            long checkpoint;

            EventStream.Subscribe<ApplicationMsgs.STSClientDetailsAdded>(this);
            EventStream.Subscribe<ApplicationMsgs.STSClientSecretAdded>(this);
            EventStream.Subscribe<ApplicationMsgs.STSClientSecretRemoved>(this);

            using (var reader = conn.GetReader(nameof(ClientStore),Handle))
            {             
                reader.Read<SecuredApplication>(()=> Idle);
                checkpoint = reader.Position ?? StreamPosition.Start;
            }
            Start<SecuredApplication>(checkpoint);
        }

        private readonly Dictionary<Guid, Client> _clientsByAppId = new Dictionary<Guid, Client>();
        private readonly Dictionary<string, Client> _clientsByClientId = new Dictionary<string, Client>();
      
        public async Task<Client> FindClientByIdAsync(string clientId)
        {
            _clientsByClientId.TryGetValue(clientId, out var client);
            return await Task.FromResult(client);
        }
        public IReadOnlyList<Client> Clients => _clientsByClientId.Values.ToList().AsReadOnly();
        public void Handle(ApplicationMsgs.STSClientDetailsAdded @event) {
            var client =
                 new Client
                 {
                     ClientId = @event.ClientId,
                     AllowedGrantTypes = @event.GrantTypes,
                     ClientSecrets = { new Secret(@event.EncryptedClientSecret.ToSha256()) },
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

        public void Handle(ApplicationMsgs.STSClientSecretAdded @event)
        {
            if (_clientsByAppId.TryGetValue(@event.ApplicationId, out var client))
            {
                client.ClientSecrets.Add(new Secret(@event.EncryptedClientSecret.ToSha256()));
            }

        }

        public void Handle(ApplicationMsgs.STSClientSecretRemoved @event)
        {
            if (_clientsByAppId.TryGetValue(@event.ApplicationId, out var client))
            {
                client.ClientSecrets.Remove(new Secret(@event.EncryptedClientSecret.ToSha256()));
            }
        }
    }
}