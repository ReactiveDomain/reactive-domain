using ReactiveDomain.Messaging;
using System;

namespace ReactiveDomain.Users.Identity
{
    public class ClientApplicationMsgs
    {
        public class CreateClientApplication : Command
        {           
            public readonly Guid ApplicationId;            
            public readonly string ApplicationDisplayName;
            public readonly string ClientSecret;
            public readonly string ClientId;

            public CreateClientApplication(
                Guid applicationId,
                string applicationDisplayName,
                string clientId,
                string clientSecret)
            {
                ApplicationId = applicationId;
                ApplicationDisplayName = applicationDisplayName;
                ClientId = clientId;
                ClientSecret = clientSecret;
            }
        }

      
        public class ClientApplicationCreated : Event
        {
          public readonly Guid ApplicationId;            
            public readonly string ApplicationDisplayName;
            public readonly string ClientSecret;
            public readonly string ClientId;

            public ClientApplicationCreated(
                Guid applicationId,
                string applicationDisplayName,
                string clientId,
                string clientSecret)
            {
                ApplicationId = applicationId;
                ApplicationDisplayName = applicationDisplayName;
                ClientId = clientId;
                ClientSecret = clientSecret;
            }
        }
        /// <summary>
        /// Define the Identity Role for ClientApplication Access
        /// </summary>
        public class DefineAccessRole : Command
        {
            public readonly Guid ClientApplicationId;
            public readonly string RoleName;
            public DefineAccessRole(
                Guid ClientApplicationId,
                string roleName)
            {
                this.ClientApplicationId = ClientApplicationId;
                RoleName = roleName;
            }
        }

        /// <summary>
        /// Identity Role for Application Access Defined
        /// </summary>
        public class AccessRoleDefined : Event
        {
            public readonly Guid ClientApplicationId;
            public readonly string RoleName;
            public AccessRoleDefined(
                Guid ClientApplicationId,
                string roleName)
            {
                this.ClientApplicationId = ClientApplicationId;
                RoleName = roleName;
            }
        }
         public class ChangeClientSecret : Command
        {            
            public readonly Guid ClientApplicationId;
            public readonly string ClientSecret;
            public ChangeClientSecret(
                Guid ClientApplicationId,
                string ClientSecret)
            {
                this.ClientApplicationId = ClientApplicationId;
                this.ClientSecret = ClientSecret;
            }
        }

        /// <summary>
        /// Identity Role for Application Access Defined
        /// </summary>
        public class ClientSecretChanged : Event
        {
            public readonly Guid ClientApplicationId;
            public readonly string ClientSecret;
            public ClientSecretChanged(
                Guid ClientApplicationId,
                string ClientSecret)
            {
                this.ClientApplicationId = ClientApplicationId;
                this.ClientSecret = ClientSecret;
            }
        }
    }
}
