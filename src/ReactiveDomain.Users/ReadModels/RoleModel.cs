using System;

namespace ReactiveDomain.Users.ReadModels {
    /// <summary>
    /// Houses the role data populated by the role created handler.
    /// </summary>
    public class RoleModel
    {
        /// <summary>
        /// The role ID.
        /// </summary>
        public Guid RoleId { get; }
        /// <summary>
        /// The role name.
        /// </summary>
        public string Name { get; }
        /// <summary>
        /// The application defining the roles.
        /// </summary>
        public ApplicationModel Application { get; }

        /// <summary>
        /// Houses the role data populated by the role created handler.
        /// </summary>
        public RoleModel(
            Guid roleId,
            string name,
            ApplicationModel application)
        {
            RoleId = roleId;
            Name = name;
            Application = application;
        }

    }
}