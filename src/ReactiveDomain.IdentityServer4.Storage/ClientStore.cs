using System.Threading.Tasks;
using IdentityServer4.Models;
using IdentityServer4.Stores;

namespace ReactiveDomain.IdentityServer4.Storage {
    public class ClientStore : IClientStore
    {
        private readonly Client _client = new Client
        {
            ClientId = "client",
            AllowedGrantTypes = GrantTypes.ClientCredentials,
            ClientSecrets = { new Secret("secret".Sha256()) },
            AllowedScopes = { "api2", "api1" }
        };
        public async Task<Client> FindClientByIdAsync(string clientId)
        {
            return await Task.FromResult(_client);
        }
    }
}