using ReactiveDomain.Messaging;
using ReactiveDomain.Util;
using System;
using System.Collections.Generic;
using static IdentityModel.OidcConstants;
using static ReactiveDomain.Users.Messages.ClientMsgs;

namespace ReactiveDomain.Users.Domain
{

    public class Client : AggregateRoot
    {
        public string ClientName { get; private set; }
        public string[] RedirectUris { get; private set; }
        public string[] LogoutRedirectUris { get; private set; }
        public string FrontChannlLogoutUri { get; private set; }

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
                FrontChannlLogoutUri = @event.FrontChannelLogoutUri;
            });
            Register<ClientSecretAdded>(@event => { });
            Register<ClientSecretRemoved>(@event => { });
        }

        ////constructor for Elbe client
        //public Client(
        //        Guid id,
        //        Guid applicationId,
        //        string clientName, //todo: value object?
        //        string encryptedClientSecret,
        //        string[] redirectUris,
        //        string[] logoutRedirectUris,
        //        string frontChannlLogoutUri,
        //        ICorrelatedMessage source)
        //        : base(source)
        //{
        //    Ensure.NotEmptyGuid(id, nameof(id));
        //    Ensure.NotEmptyGuid(applicationId, nameof(applicationId));
        //    Ensure.NotNullOrEmpty(clientName, nameof(clientName));
        //    Ensure.NotNullOrEmpty(encryptedClientSecret, nameof(encryptedClientSecret));
        //    Ensure.NotNullOrEmpty(redirectUris, nameof(frontChannlLogoutUri));
        //    Ensure.NotNullOrEmpty(logoutRedirectUris, nameof(logoutRedirectUris));
        //    Ensure.NotNullOrEmpty(frontChannlLogoutUri, nameof(redirectUris));
        //    Ensure.NotNull(source, nameof(source));
        //    //todo: move url definitions into Elbe when adding the secret store
        //    Raise(new ClientCreated(
        //          id,
        //          applicationId,
        //          clientName,
        //          new[] { "client_credentials", "password", "authorization_code" },
        //          new[] { "openid", "profile", "rd-policy", "enabled-policies" },
        //          redirectUris,
        //          logoutRedirectUris,
        //          frontChannlLogoutUri
        //          ));

        //    Raise(new ClientSecretAdded(id, encryptedClientSecret));

        //}

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
