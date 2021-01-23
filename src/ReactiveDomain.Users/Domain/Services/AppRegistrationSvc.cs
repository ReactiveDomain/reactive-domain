using System;
using System.Collections.Generic;
using System.Linq;
using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Users.Messages;
using ReactiveDomain.Users.Policy;
using ReactiveDomain.Users.ReadModels;
using ReactiveDomain.Util;

namespace ReactiveDomain.Users.Domain.Services
{
    // TODO: decide whether to merge this with ApplicationSvc.
    /// <summary>
    /// Registers application policies with the domain.
    /// </summary>
    public class AppRegistrationSvc : IConfigureSecurity
    {
        private readonly IDispatcher _bus = new Dispatcher("App Registration Bus");

        /// <summary>
        /// Configure an application version's policies. This is a one-time action per application version.
        /// </summary>
        /// <param name="definition">The policy definition of the application version to be configured.</param>
        /// <param name="conn">A connection to the ES where the policy streams are stored.</param>
        /// <exception cref="Exception">Throws if the application version has already been configured.</exception> // TODO: typed exception
        public void Configure(IPolicyDefinition definition, IStreamStoreConnection conn)
        {
            IListener GetListener() => new QueuedStreamListener(
                                            "ConfigureApplication",
                                            conn,
                                            new PrefixedCamelCaseStreamNameBuilder(),
                                            new JsonMessageSerializer());
            var appRM = new RegisteredApplicationsRM(GetListener);
            var appSvc = new ApplicationSvc(
                                new StreamStoreRepository(
                                        new PrefixedCamelCaseStreamNameBuilder(),
                                        conn,
                                        new JsonMessageSerializer()),
                                GetListener,
                                _bus);

            if (appRM.Applications.Contains(x => x.Name == definition.ApplicationName && x.Version == definition.ApplicationVersion))
                throw new Exception($"The application {definition.ApplicationName}, version {definition.ApplicationVersion} has already been configured.");

            var appId = Guid.NewGuid();
            var cmd = MessageBuilder.New(() => new ApplicationMsgs.CreateApplication(appId, definition.ApplicationName, definition.ApplicationVersion));
            _bus.Send(cmd);

            // Add all roles
            var roleIds = new Dictionary<string, Guid>();
            foreach (var role in definition.UserRoles)
            {
                var roleId = Guid.NewGuid();
                roleIds.Add(role.Name, roleId);
                _bus.Send(MessageBuilder
                              .From(cmd)
                              .Build(() => new RoleMsgs.CreateRole(
                                                roleId,
                                                role.Name,
                                                appId)));
            }

            // Add all permissions
            var permissionIds = new Dictionary<string, Guid>();
            foreach (var permission in definition.Permissions)
            {
                var permissionId = Guid.NewGuid();
                permissionIds.Add(permission, permissionId);
                _bus.Send(MessageBuilder
                              .From(cmd)
                              .Build(() => new RoleMsgs.AddPermission(
                                                permissionId,
                                                permission,
                                                appId)));
            }

            // Add role relationships and permission assignments
            foreach (var role in definition.UserRoles)
            {
                var roleId = roleIds[role.Name];
                if (role.ChildRoles.Any())
                    foreach (var childRole in role.ChildRoles) //todo: check for circles in the relationships
                    {
                        if (!roleIds.ContainsKey(childRole)) continue; //todo: should this check be earlier?
                        _bus.Send(MessageBuilder
                                      .From(cmd)
                                      .Build(() => new RoleMsgs.AssignChildRole(
                                                        roleId,
                                                        roleIds[childRole],
                                                        appId)));
                    }
                if (role.Permissions.Any())
                    foreach (var permission in role.Permissions)
                    {
                        if (!permissionIds.ContainsKey(permission)) continue;
                        _bus.Send(MessageBuilder
                                      .From(cmd)
                                      .Build(() => new RoleMsgs.AssignPermission(
                                                        roleId,
                                                        permissionIds[permission],
                                                        appId)));
                    }
            }

            appRM.Dispose();
            appSvc.Dispose();
        }
    }
}
