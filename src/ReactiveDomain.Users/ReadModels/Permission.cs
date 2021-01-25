using System;

namespace ReactiveDomain.Users.ReadModels
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
        /// The application defining the roles.
        /// </summary>
        public Application Application { get; }

        public Permission(Guid id, string name, Application application) {
            Id = id;
            Name = name;
            Application = application;
        }

        public void SetPermissionId(Guid id) {
            if(id == Id) return;
            if(id == Guid.Empty) throw new ArgumentOutOfRangeException(nameof(id),"Cannot set permissionId to guid.empty");
            if(Id != Guid.Empty) throw new InvalidOperationException("cannot change PermissionId ");
            Id = id;
        }
    }
}
