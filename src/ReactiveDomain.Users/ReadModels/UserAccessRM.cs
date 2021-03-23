using ReactiveDomain.Users.Policy;
using System;
using System.Collections.Generic;
using System.DirectoryServices.ActiveDirectory;
using System.Security.Claims;
using System.Text;

namespace ReactiveDomain.Users.ReadModels
{
    public class UserAccessRM //this might be a service or a readmodel is only read actions as it will only be called once i.e. might not need RMBase
    {       
      
        public (bool hasAccess, User applicationUser) HasAccess(ClaimsPrincipal user, SecurityPolicy accessPolicy)
        {
            //todo: check 
            //n.b. build the User object on demand after 
            throw new NotImplementedException();
            //return (false, new User(Guid.Empty,"","",""));
        }
    }
}
