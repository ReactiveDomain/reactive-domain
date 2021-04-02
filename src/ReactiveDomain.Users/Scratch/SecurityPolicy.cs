using System;

using ReactiveDomain.Foundation.Domain;

namespace ReactiveDomain.Users.Scratch
{
    /// <summary>
    /// dealproc: This should be discussed in-depth, as security policies are application specific.
    /// </summary>
    public class SecurityPolicy : ChildEntity
    {
        public SecurityPolicy(Guid policyId, string policyName, SecuredApplication root)
            : base(policyId, root)
        {
            
        }
        
        public void AddRole(Guid roleId, string roleName) { }
    }
}