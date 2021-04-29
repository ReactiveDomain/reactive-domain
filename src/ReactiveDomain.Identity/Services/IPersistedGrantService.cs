using IdentityServer4.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ReactiveDomain.IdentityServer4.Storage.Services
{
    public interface IPersistedGrantService
    {
        Task<IEnumerable<PersistedGrant>> GetAllPersistedGrant(string subjectId);
        Task<PersistedGrant> GetPersistedGrantByKey(string key);
        Task RemoveAllBySubjectIdAndClientId(string subjectId, string clientId);
        Task RemoveAllBySubjectIdAndClientIdAndType(string subjectId, string clientId, string type);
        Task RemoveAllByKey(string key);
        Task InsertPersistedGrant(PersistedGrant grant);

    }
}
