using Elbe;
using IdentityServer4;
using IdentityServer4.Models;
using IdentityServer4.Stores;
using Microsoft.Extensions.Configuration;
using PKIStsServer;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ReactiveDomain.IdentityServer4.Storage.Stores
{
    public class ClientStore : IClientStore
    {
        public IConfiguration Configuration { get; set; }

        public ClientStore(IConfiguration config)
        {
            Configuration = config;
        }

        public Task<Client> FindClientByIdAsync(string clientId)
        {
            var client = new Client
            {
                ClientId = clientId,
                ClientName = clientId,
                AllowedGrantTypes = new List<string>
                {
                    GrantTypes.Hybrid.ElementAt(0),
                    GrantTypes.ResourceOwnerPassword.ElementAt(0)
                },
                ClientSecrets = { new Secret(Constants.ClientSecret.Sha256()) },
                // where to redirect to after login
                RedirectUris = new List<string> { Constants.RedirectUri },

                // where to redirect to after logout
                PostLogoutRedirectUris = new List<string> { Constants.PostLogoutRedirectUri },

                FrontChannelLogoutUri = Constants.PostLogoutRedirectUri,
                AllowedScopes = new List<string>
                {
                        IdentityServerConstants.StandardScopes.OpenId,
                        IdentityServerConstants.StandardScopes.Profile,
                        CustomScopes.Role
                },
                RequireConsent = false,
                AccessTokenLifetime = Configuration.GetValue<int>("AppSettings:SessionTimeoutInMinutes") * 60,
            };
            return Task.FromResult(client);
        }
    }
}
