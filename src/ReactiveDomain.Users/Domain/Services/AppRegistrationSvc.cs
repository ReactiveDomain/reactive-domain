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
        //todo: Use main bus or is this full isolated? ??
        private readonly IDispatcher _bus = new Dispatcher("App Registration Bus");

        /// <summary>
        /// Configure an application version's policies. This is a one-time action per application version.
        /// </summary>
        /// <param name="definition">The policy definition of the application version to be configured.</param>
        /// <param name="conn">A connection to the ES where the policy streams are stored.</param>
        /// <exception cref="Exception">Throws if the application version has already been configured.</exception> // TODO: typed exception
        public void Configure(ISecurityPolicy policy, IStreamStoreConnection conn)
        {
            IConfiguredConnection confConn  = new ConfiguredConnection(
                                            conn,
                                            new PrefixedCamelCaseStreamNameBuilder(),
                                            new JsonMessageSerializer());

            
            var appSvc = new ApplicationSvc(
                                new StreamStoreRepository(
                                        new PrefixedCamelCaseStreamNameBuilder(),
                                        conn,
                                        new JsonMessageSerializer()),
                                ()=>confConn.GetListener(nameof(ApplicationSvc)),
                                _bus);
            //n.b. not a pure read model

            //todo:sort out the ISecurityPolicy interface, we don't want add the internal methods to the public interface 
            //and this direct cast is dangerous
            var securityRM = new SecurityPolicyRM((SecurityPolicy)policy,confConn,_bus);
/*
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
*/
        }
    }
}
