using System.Collections.Generic;
using ReactiveDomain.Users.ReadModels;

namespace ReactiveDomain.Users.Policy
{
    public class SecurityPolicy
    {
        private readonly List<Role> _roles;
        private readonly List<Permission> _permissions;
        public Application App { get; }
        public IReadOnlyList<Role> Roles => _roles.AsReadOnly();
        public IReadOnlyList<Permission> Permissions => _permissions.AsReadOnly();

        public SecurityPolicy(
            Application app,
            List<Role> roles,
            List<Permission> permissions) {
            App = app;
            _roles = roles;
            _permissions = permissions;
        }
    }
}
