using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IdentityServer4.Models;
using IdentityServer4.Stores;

namespace ReactiveDomain.IdentityServer4.Storage {
    public class ClientStore : IClientStore
    {
        private readonly List<Client> _clients = new List<Client> {

            new  Client {
                ClientId = "client",
                AllowedGrantTypes = GrantTypes.ClientCredentials,
                ClientSecrets = { new Secret("secret".Sha256()) },
                AllowedScopes = { "api2", "api1" }
            },
            new  Client {
                ClientId = "Elbe.Authentication",
                AllowedGrantTypes = GrantTypes.HybridAndClientCredentials,
                ClientSecrets = { new Secret("4BA85604-A18C-48A4-845E-0A59AA9185AE@PKI".Sha256()) },
                AllowedScopes = { "openid", "profile" },
                RedirectUris = {"http://localhost/elbe","https://localhost/elbe"},
            }

        };
        public async Task<Client> FindClientByIdAsync(string clientId) {
            var client = _clients.FirstOrDefault(c => string.CompareOrdinal(c.ClientId, clientId) ==0);
            return await Task.FromResult(client);
        }
    }
}