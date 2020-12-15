using System;
using ReactiveDomain.Util;
namespace ReactiveDomain.Users.Identity
{



    /// <summary>
    /// Aggregate for a Application.
    /// </summary>
    public class ClientApplication : AggregateRoot
    {
        private string _accessRoleName = "";
        private string _clientSecret = "";

        private ClientApplication()
        {
            RegisterEvents();
        }

        private void RegisterEvents()
        {
            Register<ClientApplicationMsgs.CreateClientApplication>(Apply);
            Register<ClientApplicationMsgs.AccessRoleDefined>(Apply);
            Register<ClientApplicationMsgs.ClientSecretChanged>(Apply);
        }

        private void Apply(ClientApplicationMsgs.CreateClientApplication evt)
        {
            Id = evt.ApplicationId;
            _clientSecret = evt.ClientSecret;
        }
        private void Apply(ClientApplicationMsgs.AccessRoleDefined evt)
        {
            _accessRoleName = evt.RoleName;
        }
        private void Apply(ClientApplicationMsgs.ClientSecretChanged evt)
        {
            _clientSecret = evt.ClientSecret;
        }

        /// <summary>
        /// Create a new Application.
        /// </summary>
        public ClientApplication(
            Guid applicationId,
            string applicationName,
            string clientid,
            string clientSecret)
            : this()
        {
            Ensure.NotEmptyGuid(applicationId, nameof(applicationId));
            Ensure.NotNullOrEmpty(applicationName, nameof(applicationName));
            Ensure.NotNullOrEmpty(clientid, nameof(clientid));
            Ensure.NotNullOrEmpty(clientSecret, nameof(clientSecret));

            Raise(new ClientApplicationMsgs.ClientApplicationCreated(applicationId, applicationName, clientid, clientSecret));
        }
        public void SetAccessRole(
            string roleName)
        {
            Ensure.NotNullOrEmpty(roleName, nameof(roleName));
           
                if (string.CompareOrdinal(roleName, _accessRoleName) == 0) { return; } //Idempotent Success                    
                throw new Exception($"Access role already set RoleName:{_accessRoleName}");
            
            Raise(new ClientApplicationMsgs.AccessRoleDefined( Id, roleName));
        }
        public void ChangeClientSecret(
           string clientSecret)
        {
            Ensure.NotNullOrEmpty(clientSecret, nameof(clientSecret));
            if (string.CompareOrdinal(clientSecret, _clientSecret) == 0) { return; } //Idempotent Success                    

            Raise(new ClientApplicationMsgs.ClientSecretChanged(Id, clientSecret));
        }
    }
}


