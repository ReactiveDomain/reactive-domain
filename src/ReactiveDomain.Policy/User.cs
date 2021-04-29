using ReactiveDomain.Users.PolicyModel;
using System;
using System.Collections.Generic;
using System.Security.Claims;

namespace ReactiveDomain.Users.PolicyModel
{
    public class User
    {
        public Guid UserId { get; }
        public string UserName { get; set; }
        public string SubjectId { get; set; }
        public string AuthDomain { get; set; }
        public Role CurrentRole { get; set; } //allow switching active role for UI etc.
        public HashSet<Role> Roles { get; } = new HashSet<Role>(); //n.b. this is a union of both role lists
        public HashSet<Role> AssignedRoles { get; } = new HashSet<Role>();
        public HashSet<Role> IdentityRoles { get; } = new HashSet<Role>();
        public List<ResourceGroup> ResourceGroups { get; } = new List<ResourceGroup>();
        public bool IsActivated { get; set; } = true;

        public readonly ClaimsPrincipal Principal;

        public User(
            Guid userId,
            string userName,
            string subjectId,
            string authDomain)
        {
            UserId = userId;
            UserName = userName;
            SubjectId = subjectId;
            AuthDomain = authDomain;
        }
    }
}