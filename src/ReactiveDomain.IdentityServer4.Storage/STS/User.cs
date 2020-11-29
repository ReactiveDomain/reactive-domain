using System.Collections.Generic;
using System.Security.Claims;

namespace ReactiveDomain.Identity.Storage.STS
{
    public class User
    {
        public User()
        {

        }
        public string SubjectId { get; set; }
        public string DomainName { get; set; }
        public string Username { get; set; }
        public string ProviderName { get; set; }
        public string ProviderSubjectId { get; set; }
        public List<Claim> Claims { get; set; }
    }
}