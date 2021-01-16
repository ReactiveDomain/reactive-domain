using System;

namespace ReactiveDomain.Users.ReadModels
{
    public class Permission {
        /// <summary>
        /// The permission ID.
        /// </summary>
        public Guid Id { get; }
        /// <summary>
        /// The permission name.
        /// </summary>
        public string Name { get; }
        /// <summary>
        /// The application defining the roles.
        /// </summary>
        public ApplicationModel Application { get; }

        public Permission(Guid id, string name, ApplicationModel application) {
            Id = id;
            Name = name;
            Application = application;
        }
      
    }
}
