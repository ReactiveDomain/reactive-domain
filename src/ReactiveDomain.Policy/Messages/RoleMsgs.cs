using System;
using ReactiveDomain.Messaging;

namespace ReactiveDomain.Policy.Messages
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
            public readonly Guid? RoleId;
            /// <summary>The name of the role.</summary>
            public readonly string Name;
            /// <summary>The policy this role applies to.</summary>
            public readonly Guid PolicyId;
            public readonly Guid AppId;

            /// <summary>
            /// Create a new Role.
            /// </summary>
            public CreateRole(
                Guid? roleId,
                string name,
                Guid policyId,
                Guid appId) {
                RoleId = roleId;
                Name = name;
                PolicyId = policyId;
                AppId = appId;
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
            /// <summary>The policy this role applies to.</summary>
            public readonly Guid PolicyId;

            /// <summary>
            /// A new role was created.
            /// </summary>
            public RoleCreated(
                Guid roleId,
                string name,
                Guid policyId)
            {
                RoleId = roleId;
                Name = name;
                PolicyId = policyId;
            }
        }
    }
}