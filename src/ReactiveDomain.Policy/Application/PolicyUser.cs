using System;
using System.Collections.Generic;
using System.Linq;
using ReactiveDomain.Users.ReadModels;

namespace ReactiveDomain.Policy.Application
{
    public class PolicyUser {
        public Guid PolicyUserId { get; }
        public UserDTO User { get; }
        public HashSet<Role> Roles { get; }
        public HashSet<string> RoleNames { get; }
        public HashSet<Type> Permissions { get; }

        public PolicyUser(Guid policyUserId, UserDTO user, IEnumerable<Role> roles, IEnumerable<Type> permissions) {
            PolicyUserId = policyUserId;
            User = user;
            Roles = new HashSet<Role>(roles);
            RoleNames = new HashSet<string>(Roles.Select(r => r.Name));
            Permissions = new HashSet<Type>(permissions);
        }
    }
}
