using System;
using System.Collections.Generic;
using System.Linq;
using Elbe.Messages;
using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging.Bus;

namespace Elbe.Domain
{
    /// <summary>
    /// Represents all of the configured roles.
    /// </summary>
    public class RolesRM :
        ReadModelBase,
        IHandle<RoleMsgs.RoleCreated>,
        IHandle<RoleMsgs.RoleMigrated>,
        IHandle<RoleMsgs.RoleRemoved>
    {
        private List<RoleModel> Roles { get; } = new List<RoleModel>();

        /// <summary>
        /// Represents all of the configured roles.
        /// </summary>
        public RolesRM(Func<IListener> getListener)
            : base(nameof(RolesRM), getListener)
        {
            // ReSharper disable once RedundantTypeArgumentsOfMethod
            EventStream.Subscribe<RoleMsgs.RoleCreated>(this);
            EventStream.Subscribe<RoleMsgs.RoleMigrated>(this);
            EventStream.Subscribe<RoleMsgs.RoleRemoved>(this);
            Start<Role>(blockUntilLive: true);
        }

        /// <summary>
        /// Given the name of the role and the application, returns whether the role exists or not.
        /// </summary>
        public bool RoleExists(
            string name,
            string application)
        {
            return Roles.Any(x => x.Name == name && x.Application == application);
        }

        /// <summary>
        /// Gets the unique ID of the specified role.
        /// </summary>
        /// <param name="name">The of the role.</param>
        /// <param name="application">The application for which this role is defined.</param>        
        /// <param name="id">The unique ID of the role. This is the out parameter</param>
        /// <returns>True if a role with matching properties was found, otherwise false.</returns>
        public bool TryGetRoleId(
            string name,
            string application,
            out Guid id)
        {
            id = Guid.Empty;
            var role = Roles.FirstOrDefault(x => x.Name == name && x.Application == application);
            if (role != null)
                id = role.RoleId;
            return id != Guid.Empty;
        }

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
            public string Application { get; }

            /// <summary>
            /// Houses the role data populated by the role created handler.
            /// </summary>
            public RoleModel(
                Guid roleId,
                string name,
                string application)
            {
                RoleId = roleId;
                Name = name;
                Application = application;
            }

        }

        /// <summary>
        /// Given the role created event, adds a new role to the collection of roles.
        /// </summary>
        public void Handle(RoleMsgs.RoleCreated message)
        {
            if (Roles.Any(role => role.RoleId == message.RoleId))
            {
                return;
            }

            Roles.Add(new RoleModel(
                            message.RoleId,
                            message.Name,
                            message.Application));
        }

        /// <summary>
        /// Given the role created event, adds a new role to the collection of roles.
        /// </summary>
        public void Handle(RoleMsgs.RoleMigrated message)
        {
            if (Roles.Any(role => role.RoleId == message.RoleId))
            {
                return;
            }
            Roles.Add(new RoleModel(
                            message.RoleId,
                            message.Name,
                            message.Application));
        }

        /// <summary>
        /// Given the role created event, adds a new role to the collection of roles.
        /// </summary>
        public void Handle(RoleMsgs.RoleRemoved message)
        {
            Roles.RemoveAll(role => role.RoleId == message.RoleId);
        }
    }
}

