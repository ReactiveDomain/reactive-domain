using IdentityServer4.Models;
using IdentityServer4.Stores;
using ReactiveDomain.IdentityServer4.Storage.Services;
using System.Collections.Generic;
using System.Threading.Tasks;


namespace ReactiveDomain.IdentityServer4.Storage.Stores
{
    public class PersistedGrantStore : IPersistedGrantStore
    {
        private readonly IPersistedGrantService _grantService;
        public PersistedGrantStore(IPersistedGrantService grantService)
        {
            _grantService = grantService;
        }
        public async Task<IEnumerable<PersistedGrant>> GetAllAsync(string subjectId)
        {
            return await _grantService.GetAllPersistedGrant(subjectId);
        }

        public async Task<PersistedGrant> GetAsync(string key)
        {
            return await _grantService.GetPersistedGrantByKey(key);
        }

        public async Task<IEnumerable<PersistedGrant>> GetAllAsync(PersistedGrantFilter filter)
        {
            throw new System.NotImplementedException();
        }

        public async Task RemoveAllAsync(string subjectId, string clientId)
        {
            await _grantService.RemoveAllBySubjectIdAndClientId(subjectId, clientId);
        }

        public async Task RemoveAllAsync(string subjectId, string clientId, string type)
        {
            await _grantService.RemoveAllBySubjectIdAndClientIdAndType(subjectId, clientId, type);
        }

        public async Task RemoveAsync(string key)
        {
            await _grantService.RemoveAllByKey(key);
        }

        public async Task RemoveAllAsync(PersistedGrantFilter filter)
        {
            throw new System.NotImplementedException();
        }

        public async Task StoreAsync(PersistedGrant grant)
        {
            await _grantService.InsertPersistedGrant(grant);
        }
    }
}
