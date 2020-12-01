using System.Collections.Generic;
using System.Security.Claims;

namespace ReactiveDomain.Users.Identity
{
    public class IdentityUser
    {
        public string SubjectId { get; set; }
        public string DomainName { get; set; }
        public string Username { get; set; }
        public string ProviderName { get; set; }
        public string ProviderSubjectId { get; set; }
        public List<Claim> Claims { get; set; }
    }
}
