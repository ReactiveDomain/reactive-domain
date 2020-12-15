using System;

namespace ReactiveDomain.Users.Identity
{
    public class ClientDTO
    {
        public Guid ClientApplicationId { get;  }
        public string ClientApplicationName{ get; }
        public string ClientId { get;  }
        public string AccessRole { get; set; }
        public string ClientSecret { get; set; }
        public ClientDTO(
            Guid clientApplicationId,
            string clientApplicationName,
            string clientId, 
            string accessRole, 
            string clientSecret)
        {
            ClientApplicationId = clientApplicationId;
            ClientApplicationName = clientApplicationName;
            ClientId = clientId;
            AccessRole = accessRole;
            ClientSecret = clientSecret;
        }
    }
}
