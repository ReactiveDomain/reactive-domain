using System;
using System.Collections.Generic;

namespace ReactiveDomain.Users.Identity
{
    public class SubjectDTO
    {
        public Guid SubjectId { get;  }
        public string ProviderSubClaim { get;  }
        public HashSet<string> Roles { get;  }
        public SubjectDTO(
            Guid subjectId, 
            string providerSubClaim)
        {
            SubjectId = subjectId;
            ProviderSubClaim = providerSubClaim;
            Roles = new HashSet<string>();
        }
    }
}
