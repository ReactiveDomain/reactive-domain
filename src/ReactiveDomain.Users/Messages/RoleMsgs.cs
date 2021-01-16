using System;
using ReactiveDomain.Messaging;

namespace ReactiveDomain.Users.Messages
{
    /// <summary>
    /// Messages for the Role domain.
    /// </summary>
    public class RoleMsgs
    {
        /// <summary>
        /// Create a new Role.
        /// </summary>
        public class CreateRole : Command
        {
            /// <summary>The unique ID of the new role.</summary>
            public readonly Guid RoleId;
            /// <summary>The name of the role.</summary>
            public readonly string Name;
            /// <summary>The application this role applies to.</summary>
            public readonly Guid ApplicationId;


            /// <summary>
            /// Create a new Role.
            /// </summary>
            public CreateRole(
                Guid roleId,
                string name,
                Guid applicationId)
            {
                RoleId = roleId;
                Name = name;
                ApplicationId = applicationId;
            }

        }

        /// <summary>
        /// A new role was created.
        /// </summary>
        public class RoleCreated : Event
        {
            /// <summary>The unique ID of the new role.</summary>
            public readonly Guid RoleId;
            /// <summary>The name of the role.</summary>
            public readonly string Name;
            /// <summary>The application this role applies to.</summary>
            public readonly Guid ApplicationId;

            /// <summary>
            /// A new role was created.
            /// </summary>
            public RoleCreated(
                Guid roleId,
                string name,
                Guid applicationId)
            {
                RoleId = roleId;
                Name = name;
                ApplicationId = applicationId;
            }

        }
        /// <summary>
        /// Add an existing role as child role to the parent
        /// </summary>
        public class AssignChildRole : Command
        {
            /// <summary>The Id of the parent role.</summary>
            public readonly Guid ParentRoleId;
            /// <summary>The Id of the child role.</summary>
            public readonly Guid ChildRoleId;
            /// <summary>The application these role apply to.</summary>
            public readonly Guid ApplicationId;

            /// <summary>
            /// Add an existing role as child role to the parent
            /// </summary>
            public AssignChildRole(
                Guid parentRoleId,
                Guid childRoleId,
                Guid applicationId)
            {
                ParentRoleId = parentRoleId;
                ChildRoleId = childRoleId;
                ApplicationId = applicationId;
            }

        }

        /// <summary>
        /// An existing role was added as child role to the parent
        /// </summary>
        public class ChildRoleAssigned : Event
        {
            /// <summary>The Id of the parent role.</summary>
            public readonly Guid ParentRoleId;
            /// <summary>The Id of the child role.</summary>
            public readonly Guid ChildRoleId;
            /// <summary>The application these role apply to.</summary>
            public readonly Guid ApplicationId;

            /// <summary>
            /// An existing role was added as child role to the parent
            /// </summary>
            public ChildRoleAssigned(
                Guid parentRoleId,
                Guid childRoleId,
                Guid applicationId)
            {
                ParentRoleId = parentRoleId;
                ChildRoleId = childRoleId;
                ApplicationId = applicationId;
            }

        }
        /// <summary>
        /// Add a permission
        /// </summary>
        public class AddPermission : Command
        {
            /// <summary>The Id of the permission.</summary>
            public readonly Guid PermissionId;
            /// <summary>The type name of the permission.</summary>
            public readonly string PermissionName;
            /// <summary>The application these role apply to.</summary>
            public readonly Guid ApplicationId;

            /// <summary>
            /// Add a permission
            /// </summary>
            public AddPermission(
                Guid permissionId,
                string permissionName,
                Guid applicationId) {
                PermissionId = permissionId;
                PermissionName = permissionName;
                ApplicationId = applicationId;
            }

        }

        /// <summary>
        /// Permission added
        /// </summary>
        public class PermissionAdded : Event
        {
            /// <summary>The Id of the permission.</summary>
            public readonly Guid PermissionId;
            /// <summary>The type name of the permission.</summary>
            public readonly string PermissionName;
            /// <summary>The application these role apply to.</summary>
            public readonly Guid ApplicationId;

            /// <summary>
            /// Permission added
            /// </summary>
            public PermissionAdded(
                Guid permissionId,
                string permissionName,
                Guid applicationId)
            {
                PermissionId = permissionId;
                PermissionName = permissionName;
                ApplicationId = applicationId;
            }

        }
        /// <summary>
        /// Assign a permission to a role
        /// </summary>
        public class AssignPermission : Command
        {
            /// <summary>The Id of the role.</summary>
            public readonly Guid RoleId;
            /// <summary>The Id of the permission.</summary>
            public readonly Guid PermissionId;
            /// <summary>The application these role apply to.</summary>
            public readonly Guid ApplicationId;

            /// <summary>
            /// Add a permission to the role
            /// </summary>
            public AssignPermission(
                Guid roleId,
                Guid permissionId,
                Guid applicationId)
            {
                RoleId = roleId;
                PermissionId = permissionId;
                ApplicationId = applicationId;
            }

        }

        /// <summary>
        /// Permission Assigned to a role
        /// </summary>
        public class PermissionAssigned : Event
        {
            /// <summary>The Id of the role.</summary>
            public readonly Guid RoleId;
            /// <summary>The Id of the permission.</summary>
            public readonly Guid PermissionId;
            /// <summary>The application these role apply to.</summary>
            public readonly Guid ApplicationId;

            /// <summary>
            /// Permission added to a role
            /// </summary>
            public PermissionAssigned(
                Guid roleId,
                Guid permissionId,
                Guid applicationId)
            {
                RoleId = roleId;
                PermissionId = permissionId;
                ApplicationId = applicationId;
            }

        }

        //todo: fix migration to match the new model
        /// <summary>
        /// Role data was migrated.
        /// </summary>
        public class RoleMigrated : Event
        {
            /// <summary>The unique ID of the new role.</summary>
            public readonly Guid RoleId;
            /// <summary>The name of the role.</summary>
            public readonly string Name;
            /// <summary>The application this role applies to.</summary>
            public readonly string Application;
            /// <summary>The source stream.</summary>
            public readonly string Source;
            /// <summary> The number of Events migrated.</summary>
            public readonly int EventCount;

            /// <summary>
            /// Role data was migrated.
            /// </summary>
            public RoleMigrated( 
                Guid roleId,
                string name,
                string application,
                string source, 
                int eventCount)
            {
                RoleId = roleId;
                Name = name;
                Application = application;
                Source = source;
                EventCount = eventCount;
            }

        }
      
        /// <summary>
        /// Role data migrated.
        /// </summary>
        public class RoleDataMigrated : Event
        {
            /// <summary>The unique ID of the role.</summary>
            public readonly Guid RoleId;
            /// <summary>The stream data migrated to.</summary>
            public readonly string TargetStream;

            /// <summary>
            /// Role data migrated.
            /// </summary>
            public RoleDataMigrated(Guid roleId, string targetStream)
            {
                RoleId = roleId;
                TargetStream = targetStream;
            }
        }
    }
}