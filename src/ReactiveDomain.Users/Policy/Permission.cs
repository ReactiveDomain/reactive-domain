using System;

namespace ReactiveDomain.Users.Policy
{
    public class Permission {
        /// <summary>
        /// The permission ID.
        /// </summary>
        public Guid Id { get; private set; }
        /// <summary>
        /// The permission name.
        /// </summary>
        public string Name { get; }
        /// <summary>
        /// The PolicyId defining the roles.
        /// </summary>
        public Guid PolicyId { get; }

        public Permission(Guid id, string name, Guid policyId) {
            Id = id;
            Name = name;
            PolicyId = policyId;
        }

        public void SetPermissionId(Guid id) {
            if(id == Id) return;
            if(id == Guid.Empty) throw new ArgumentOutOfRangeException(nameof(id),"Cannot set permissionId to guid.empty");
            if(Id != Guid.Empty) throw new InvalidOperationException("cannot change PermissionId ");
            Id = id;
        }
    }
}
