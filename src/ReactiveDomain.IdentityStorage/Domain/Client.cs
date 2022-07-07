using System;
using ReactiveDomain.Messaging;
using ReactiveDomain.Util;
using static ReactiveDomain.IdentityStorage.Messages.ClientMsgs;

namespace ReactiveDomain.IdentityStorage.Domain
{
    public class Client : AggregateRoot
    {
        public string ClientName { get; private set; }
        public string[] RedirectUris { get; private set; }
        public string[] LogoutRedirectUris { get; private set; }
        public string FrontChannelLogoutUri { get; private set; }

        private Client()
        {
            RegisterEvents();
        }

        private void RegisterEvents()
        {
            Register<ClientCreated>(@event =>
            {
                Id = @event.ClientId;
                ClientName = @event.ClientName;
                RedirectUris = @event.RedirectUris;
                LogoutRedirectUris = @event.PostLogoutRedirectUris;
                FrontChannelLogoutUri = @event.FrontChannelLogoutUri;
            });
            Register<ClientSecretAdded>(@event => { });
            Register<ClientSecretRemoved>(@event => { });
        }

        /// <summary>
        /// Create a new client registration for identity server.
        /// </summary>
        public Client(
                Guid id,
                Guid applicationId,
                string clientName,
                string encryptedClientSecret,
                string[] redirectUris,
                string[] postLogoutRedirectUris,
                string frontChannelLogoutUri,
                ICorrelatedMessage source)
                : base(source)
        {
            Ensure.NotEmptyGuid(id, nameof(id));
            Ensure.NotEmptyGuid(applicationId, nameof(applicationId));
            Ensure.NotNullOrEmpty(clientName, nameof(clientName));
            Ensure.NotNullOrEmpty(encryptedClientSecret, nameof(encryptedClientSecret));

            RegisterEvents();

            Raise(new ClientCreated(
                id,
                applicationId,
                clientName,
                new[] { "client_credentials", "password", "authorization_code" },
                new[] { "openid", "profile", "rd-policy", "enabled-policies" },
                redirectUris,
                postLogoutRedirectUris,
                frontChannelLogoutUri));

            Raise(new ClientSecretAdded(id, encryptedClientSecret));
        }
        public void AddClientSecret(string encryptedClientSecret)
        {
            //todo: when adding encryption, make this idempotent
            Ensure.NotNullOrEmpty(encryptedClientSecret, nameof(encryptedClientSecret));
            Raise(new ClientSecretAdded(Id, encryptedClientSecret));
        }
        public void RemoveClientSecret(string encryptedClientSecret)
        {
            //todo: when adding encryption, check for existence
            Ensure.NotNullOrEmpty(encryptedClientSecret, nameof(encryptedClientSecret));
            Raise(new ClientSecretRemoved(Id, encryptedClientSecret));
        }
    }
}
