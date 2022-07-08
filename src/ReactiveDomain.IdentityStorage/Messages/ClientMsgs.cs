using System;
using ReactiveDomain.Messaging;

namespace ReactiveDomain.IdentityStorage.Messages
{
    public class ClientMsgs
    {
        public class CreateClient : Command
        {
            public readonly Guid ClientId;
            public readonly Guid ApplicationId;
            public readonly string ClientName;
            public readonly string[] RedirectUris;
            public readonly string[] PostLogoutRedirectUris;
            public readonly string FrontChannelLogoutUri;
            public readonly string EncryptedClientSecret;

            public CreateClient(
                Guid clientId,
                Guid applicationId,
                string clientName,
                string[] redirectUris,
                string[] postLogoutRedirectUris,
                string frontChannelLogoutUri,
                string encryptedClientSecret)
            {
                ClientId = clientId;
                ApplicationId = applicationId;
                ClientName = clientName;
                RedirectUris = redirectUris;
                PostLogoutRedirectUris = postLogoutRedirectUris;
                FrontChannelLogoutUri = frontChannelLogoutUri;
                EncryptedClientSecret = encryptedClientSecret;
            }
        }

        public class ClientCreated : Event
        {
            public readonly Guid ClientId;
            public readonly Guid ApplicationId;
            public readonly string ClientName;
            public readonly string[] GrantTypes;
            public readonly string[] AllowedScopes;
            public readonly string[] RedirectUris;
            public readonly string[] PostLogoutRedirectUris;
            public readonly string FrontChannelLogoutUri;

            public ClientCreated(
                Guid clientId,
                Guid applicationId,
                string clientName,
                string[] grantTypes,
                string[] allowedScopes,
                string[] redirectUris,
                string[] postLogoutRedirectUris,
                string frontChannelLogoutUri)
            {
                ClientId = clientId;
                ApplicationId = applicationId;
                ClientName = clientName;
                GrantTypes = grantTypes;
                AllowedScopes = allowedScopes;
                RedirectUris = redirectUris;
                PostLogoutRedirectUris = postLogoutRedirectUris;
                FrontChannelLogoutUri = frontChannelLogoutUri;
            }
        }

        public class AddClientSecret : Command
        {
            public readonly Guid ClientId;
            public readonly string EncryptedClientSecret;

            public AddClientSecret(Guid clientId, string encryptedClientSecret)
            {
                ClientId = clientId;
                EncryptedClientSecret = encryptedClientSecret;
            }
        }

        public class ClientSecretAdded : Event
        {
            public readonly Guid ClientId;
            public readonly string EncryptedClientSecret;

            public ClientSecretAdded(Guid clientId, string encryptedClientSecret)
            {
                ClientId = clientId;
                EncryptedClientSecret = encryptedClientSecret;
            }
        }

        public class RemoveClientSecret : Command
        {
            public readonly Guid ClientId;
            public readonly string EncryptedClientSecret;

            public RemoveClientSecret(Guid clientId, string encryptedClientSecret)
            {
                ClientId = clientId;
                EncryptedClientSecret = encryptedClientSecret;
            }
        }

        public class ClientSecretRemoved : Event
        {
            public readonly Guid ClientId;
            public readonly string EncryptedClientSecret;

            public ClientSecretRemoved(Guid clientId, string encryptedClientSecret)
            {
                ClientId = clientId;
                EncryptedClientSecret = encryptedClientSecret;
            }
        }
    }
}
