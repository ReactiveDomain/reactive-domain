using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Users.Domain.Aggregates;
using ReactiveDomain.Users.Domain.Services;
using ReactiveDomain.Users.Messages;
using ReactiveDomain.Users.ReadModels;

namespace ReactiveDomain.Users.Policy
{
    // for use in applications enforcing security policy
    /// <summary>
    /// A read model that contains a synchronized security policy for the application. 
    /// </summary>
    public class SecurityPolicySyncService :
        ReadModelBase,
        IHandle<ApplicationMsgs.ApplicationCreated>,
        IHandle<ApplicationMsgs.PolicyCreated>,
        IHandle<RoleMsgs.RoleCreated>,
        IHandle<RoleMsgs.RoleMigrated>,
        //IHandle<UserMsgs.UserCreated>,
        //IHandle<UserMsgs.Deactivated>,
        //IHandle<UserMsgs.Activated>,
        //IHandle<UserMsgs.RoleAssigned>,
        //IHandle<UserMsgs.AuthDomainUpdated>,
        //IHandle<UserMsgs.UserNameUpdated>,
        //IHandle<UserMsgs.RoleUnassigned>, 
        IHandle<RoleMsgs.ChildRoleAssigned>,
        IHandle<RoleMsgs.PermissionAdded>,
        IHandle<RoleMsgs.PermissionAssigned>
    {
        //public data
        public SecurityPolicy Policy;

        private readonly Dictionary<Guid, Permission> _permissions = new Dictionary<Guid, Permission>();
        private readonly Dictionary<Guid, Role> _roles = new Dictionary<Guid, Role>();
        private readonly Dictionary<Guid, SecuredApplication> _applications = new Dictionary<Guid, SecuredApplication>();


        /// <summary>
        /// Create a read model for a synchronized Security Policy.
        /// </summary>
        public SecurityPolicySyncService(
            SecurityPolicy basePolicy,
            IConfiguredConnection conn)
            : base(nameof(ApplicationsRM), () => conn.GetListener(nameof(SecurityPolicySyncService)))
        {
            var dispatcher = new Dispatcher(nameof(SecurityPolicySyncService));
            var appSvc = new ApplicationSvc(conn, dispatcher);

            Policy = basePolicy ?? new SecurityPolicyBuilder().Build();

            using (var appReader = conn.GetReader("AppReader"))
            {
                appReader.EventStream.Subscribe<ApplicationMsgs.ApplicationCreated>(this);
                appReader.Read(typeof(ApplicationMsgs.ApplicationCreated));
            }
            Guid appId;
            Guid correlationId = Guid.NewGuid();
            var dbApp = _applications.Values.FirstOrDefault(
                app => string.CompareOrdinal(app.Name, Policy.ApplicationName) == 0 &&
                                string.CompareOrdinal(app.Version, Policy.ApplicationVersion) == 0);
            if (dbApp == null)
            {
                appId = Guid.NewGuid();
                var appCmd = new ApplicationMsgs.CreateApplication(appId, Policy.ApplicationName, Policy.ApplicationVersion) { CorrelationId = correlationId };
                dispatcher.Send(appCmd);
                var policyId = Guid.NewGuid();
                var polCmd = new ApplicationMsgs.CreatePolicy(policyId, Policy.PolicyName, appId) { CorrelationId = correlationId };
                dispatcher.Send(polCmd);
                Policy.OwningApplication.UpdateApplicationDetails(appId);
                Policy.PolicyId = policyId;
            }
            else
            {
                appId = dbApp.Id;
            }

            var policySvc = new PolicySvc(appId, conn, dispatcher);

            
            using (var appReader = conn.GetReader("RoleReader"))
            {
                appReader.EventStream.Subscribe<ApplicationMsgs.ApplicationCreated>(this);
                appReader.EventStream.Subscribe<ApplicationMsgs.PolicyCreated>(this);
                appReader.EventStream.Subscribe<RoleMsgs.RoleCreated>(this);
                appReader.EventStream.Subscribe<RoleMsgs.PermissionAdded>(this);
                appReader.EventStream.Subscribe<RoleMsgs.PermissionAssigned>(this);
                appReader.EventStream.Subscribe<RoleMsgs.ChildRoleAssigned>(this);

                appReader.Read<SecuredApplicationAgg>(appId);
            }
            if (dbApp == null) {
                dbApp = _applications[appId];
            }
            Policy.OwningApplication.UpdateApplicationDetails(appId);
            Policy.PolicyId = dbApp.Policies.First(p => p.PolicyName.Equals(Policy.PolicyName)).PolicyId;

            //enrich db with roles from the base policy, if any are missing
            foreach (var role in Policy.Roles)
            {
                if (_roles.Values.All(r => !r.Name.Equals(role.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    var roleId = Guid.NewGuid();
                    var cmd = new RoleMsgs.CreateRole(roleId, role.Name, Policy.PolicyId) { CorrelationId = correlationId };
                    dispatcher.Send(cmd);
                    role.SetRoleId(roleId);
                    _roles.Add(roleId, new Role(roleId, role.Name, role.PolicyId));
                }
            }
            //sync all roles on the Policy with Ids from the DB
            //and add any missing roles from the db
            foreach (var role in _roles.Values)
            {
                var baseRole = Policy.Roles.FirstOrDefault(r => r.Name.Equals(role.Name, StringComparison.OrdinalIgnoreCase));
                if (baseRole == null)
                {
                    Policy.AddRole(role);
                }
                else
                {
                    baseRole.SetRoleId(role.RoleId);
                }
            }
            //enrich db with permissions from the base policy, if any are missing
            foreach (var permission in Policy.Permissions)
            {
                var dbPerm = _permissions.Values.FirstOrDefault(p =>
                    p.Name.Equals(permission.Name, StringComparison.OrdinalIgnoreCase));
                if (dbPerm == null)
                {
                    var permissionId = Guid.NewGuid();
                    var cmd = new RoleMsgs.AddPermission(permissionId, permission.Name, Policy.PolicyId) { CorrelationId = correlationId };
                    dispatcher.Send(cmd);
                    permission.SetPermissionId(permissionId);
                    _permissions.Add(permissionId, permission);
                }
                else
                {
                    permission.SetPermissionId(dbPerm.Id);
                }

            }
            //sync all permissions on the Policy with Ids from the DB
            //and add any missing permissions from the db
            foreach (var permission in _permissions.Values)
            {
                var basePermission = Policy.Permissions.FirstOrDefault(p => p.Name.Equals(permission.Name, StringComparison.OrdinalIgnoreCase));
                if (basePermission == null)
                {
                    Policy.AddPermission(permission);
                }
                else
                {
                    basePermission.SetPermissionId(permission.Id);
                }
            }
            //enrich db with permission assignments from the base policy, if any are missing
            foreach (var role in Policy.Roles)
            {
                var dbRole = _roles[role.RoleId];
                foreach (var permission in role.DirectPermissions)
                {
                    var dbPermission = dbRole.DirectPermissions.FirstOrDefault(p => p.Id == permission.Id);
                    if (dbPermission == null)
                    {
                        var cmd = new RoleMsgs.AssignPermission(dbRole.RoleId, permission.Id, Policy.PolicyId) { CorrelationId = correlationId };
                        dispatcher.Send(cmd);
                        dbRole.AddPermission(permission);
                    }
                }
            }
            //sync db permission assignments into the base policy, if any are missing
            foreach (var role in _roles.Values)
            {
                var baseRole = Policy.Roles.First(r => r.RoleId == role.RoleId);
                foreach (var permission in role.DirectPermissions)
                {
                    if (baseRole.DirectPermissions.All(p => p.Id != permission.Id))
                    {
                        baseRole.AddPermission(permission);
                    }
                }
            }
            //enrich db with child role assignments from the base policy, if any are missing
            foreach (var role in Policy.Roles)
            {
                var dbRole = _roles[role.RoleId];
                foreach (var childRole in role.ChildRoles)
                {
                    var dbChildRole = dbRole.ChildRoles.FirstOrDefault(cr => cr.RoleId == childRole.RoleId);
                    if (dbChildRole == null)
                    {
                        var cmd = new RoleMsgs.AssignChildRole(dbRole.RoleId, childRole.RoleId, Policy.PolicyId) { CorrelationId = correlationId };
                        dispatcher.Send(cmd);
                        dbRole.AddChildRole(childRole);
                    }
                }
            }

            //sync db child role assignments into the base policy, if any are missing
            foreach (var role in _roles.Values)
            {
                var baseRole = Policy.Roles.First(r => r.RoleId == role.RoleId);
                foreach (var childRole in role.ChildRoles)
                {
                    if (baseRole.ChildRoles.All(cr => cr.RoleId != childRole.RoleId))
                    {
                        baseRole.AddChildRole(childRole);
                    }
                }
            }
            //todo: subscribe to user assignments

            //todo: subscribe to user stream???
            //Start<ApplicationRoot>(appId, blockUntilLive: true);
        }

        public void Handle(ApplicationMsgs.ApplicationCreated @event)
        {
            if (_applications.ContainsKey(@event.ApplicationId)) return;

            _applications.Add(
                @event.ApplicationId,
                new SecuredApplication(
                    @event.ApplicationId,
                    @event.Name,
                    @event.ApplicationVersion
                ));
        }

        public void Handle(ApplicationMsgs.PolicyCreated @event)
        {
            if (!_applications.ContainsKey(@event.ApplicationId)) return;
            var app = _applications[@event.ApplicationId];
            app.AddPolicy(new SecurityPolicy(@event.PolicyName, @event.PolicyId, app));
        }

        /// <summary>
        /// Given the role created event, adds a new role to the collection of roles.
        /// </summary>
        public void Handle(RoleMsgs.RoleCreated @event)
        {
            if (_roles.ContainsKey(@event.RoleId)) return;
            _roles.Add(
                @event.RoleId,
                new Role(
                    @event.RoleId,
                    @event.Name,
                    Policy.PolicyId));
        }
        //todo: handle migration events
        /// <summary>
        /// Given the role migrated event, adds the role to the collection of roles.
        /// </summary>
        public void Handle(RoleMsgs.RoleMigrated @event)
        {
            /*
            if(_roles.ContainsKey(@event.RoleId)) return;
            if(!_applications.ContainsKey(@event.ApplicationId)) return; //todo: log error, this should never happen
            var app = _applications[@event.ApplicationId];
            
            _roles.Add(
                @event.RoleId,
                new RoleModel(
                    @event.RoleId,
                    @event.Name,
                    app));
            */
        }

        public void Handle(RoleMsgs.ChildRoleAssigned @event)
        {
            if (!_roles.ContainsKey(@event.ParentRoleId)) return;
            if (!_roles.ContainsKey(@event.ChildRoleId)) return;

            _roles[@event.ParentRoleId].AddChildRole(_roles[@event.ChildRoleId]);
        }
        public void Handle(RoleMsgs.PermissionAdded @event)
        {
            if (_permissions.ContainsKey(@event.PermissionId)) return;
            _permissions.Add(@event.PermissionId, new Permission(@event.PermissionId, @event.PermissionName, Policy.PolicyId));
        }
        public void Handle(RoleMsgs.PermissionAssigned @event)
        {
            if (!_roles.ContainsKey(@event.RoleId)) return; //todo: log this error
            if (!_permissions.ContainsKey(@event.PermissionId)) return; //todo: log this error
            _roles[@event.RoleId].AddPermission(_permissions[@event.PermissionId]);
        }

        /*
        /// <summary>
        /// Handle a UserMsgs.UserCreated event.
        /// </summary>
        public void Handle(UserMsgs.UserCreated @event) {
            if (_users.ContainsKey(@event.Id)) return;
            _users.Add(
                @event.Id,
                new UserModel(
                    @event.Id,
                    @event.UserName,
                    @event.SubjectId,
                    @event.AuthDomain));
        }
        public void Handle(UserMsgs.Deactivated @event)
        {
            if (!_users.ContainsKey(@event.UserId)) return;
            _users[@event.UserId].IsActivated = false;
        }

        public void Handle(UserMsgs.Activated @event)
        {
            if (!_users.ContainsKey(@event.UserId)) return;
            _users[@event.UserId].IsActivated = true;
        }

        public void Handle(UserMsgs.RoleAssigned @event)
        {
            if (!_users.ContainsKey(@event.UserId) || !_roles.ContainsKey(@event.RoleId)) return;
            var role = _users[@event.UserId].Roles.FirstOrDefault(r => r.RoleId == @event.RoleId);
            if( role != null) return;
            _users[@event.UserId].Roles.Add(_roles[@event.RoleId]);
        }

        public void Handle(UserMsgs.RoleUnassigned @event)
        {
            if (!_users.ContainsKey(@event.UserId)|| !_roles.ContainsKey(@event.RoleId)) return;
            var role = _users[@event.UserId].Roles.FirstOrDefault(r => r.RoleId == @event.RoleId);
            if( role == null) return;
            _users[@event.UserId].Roles.Remove(role);
        }
        public void Handle(UserMsgs.AuthDomainUpdated @event)
        {
            if (!_users.ContainsKey(@event.UserId)) return;
            _users[@event.UserId].AuthDomain = @event.AuthDomain;
        }
        public void Handle(UserMsgs.UserNameUpdated @event)
        {
            if (!_users.ContainsKey(@event.UserId)) return;
            _users[@event.UserId].UserName = @event.UserName;
        }
        public List<Role> RolesForUser(
            string subjectId,
            string userName,
            string authDomain,
            Application application)
        {
            var user = _users.Values.FirstOrDefault(x =>
                x.AuthDomain.Equals(authDomain, StringComparison.CurrentCultureIgnoreCase)
                && (string.IsNullOrEmpty(subjectId) ||
                    string.IsNullOrEmpty(x.SubjectId)
                    ? x.UserName.Equals(userName, StringComparison.CurrentCultureIgnoreCase)
                    : x.SubjectId.Equals(subjectId, StringComparison.CurrentCultureIgnoreCase))

            );
            if (user == null)
            {
                throw new UserNotFoundException("UserId = " + subjectId + ", authDomain = " + authDomain);
            }
            if (!user.IsActivated)
            {
                throw new UserDeactivatedException("UserId = " + subjectId + ", authDomain = " + authDomain);
            }
            return user.Roles.FindAll(x => x.Application == application);
        }
        */

    }
}
