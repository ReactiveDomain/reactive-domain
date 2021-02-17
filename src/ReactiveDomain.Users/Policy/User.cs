using System;
using System.Collections.Generic;
using System.Security.Claims;

namespace ReactiveDomain.Users.Policy {
    public class User
    {
        public Guid UserId { get; }
        public string UserName { get; set; }
        public string SubjectId { get; set; }
        public string AuthDomain { get; set; }
        public Role CurrentRole { get; set; } //allow switching active role for UI etc.
        public List<Role> Roles { get; } = new List<Role>(); //n.b. this is a union of both role lists
        public List<Role> AssignedRoles { get; } = new List<Role>();
        public List<Role> IdentityRoles { get; } = new List<Role>();
        public List<Permission> Permissions { get; } = new List<Permission>();
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