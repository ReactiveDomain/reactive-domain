using IdentityServer4.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;


namespace ReactiveDomain.IdentityServer4.Storage.Services
{
    public class PersistedGrantService : IPersistedGrantService
    {
        readonly List<PersistedGrant> _persistedGrants = new List<PersistedGrant>();
        
        public async Task<IEnumerable<PersistedGrant>> GetAllPersistedGrant(string subjectId)
        {
            return await Task.Run(() => _persistedGrants.FindAll(g => g.SubjectId == subjectId));
        }

        public async Task<PersistedGrant> GetPersistedGrantByKey(string key)
        {
            return await Task.Run(() => _persistedGrants.FirstOrDefault(g => g.Key == key));
        }

        public async Task RemoveAllBySubjectIdAndClientId(string subjectId, string clientId)
        {
            await Task.Factory.StartNew(() =>
            {
                _persistedGrants.RemoveAll(g => g.SubjectId == subjectId && g.ClientId == clientId);
            });
        }

        public async Task RemoveAllBySubjectIdAndClientIdAndType(string subjectId, string clientId, string type)
        {
            await Task.Factory.StartNew(() =>
            {
                _persistedGrants.RemoveAll(g => g.SubjectId == subjectId && g.ClientId == clientId && g.Type == type);
            });

        }

        public async Task RemoveAllByKey(string key)
        {
            await Task.Factory.StartNew(() =>
            {
                _persistedGrants.RemoveAll(g => g.Key == key);
            });

        }

        public async Task InsertPersistedGrant(PersistedGrant grant)
        {
            await Task.Factory.StartNew(() =>
            {
                _persistedGrants.Add(grant);
            });
        }
    }
}