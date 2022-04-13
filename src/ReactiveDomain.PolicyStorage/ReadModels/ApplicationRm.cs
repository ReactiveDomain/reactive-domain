using DynamicData;
using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Policy.Messages;
using System;
using System.Collections.Generic;
using System.Linq;


namespace ReactiveDomain.Policy.ReadModels
{
    public class ApplicationRm :
        ReadModelBase,
        IHandle<ApplicationMsgs.ApplicationCreated>,
        IHandle<ApplicationMsgs.PolicyCreated>,
        IHandle<ApplicationMsgs.RoleCreated>,
        IHandle<PolicyUserMsgs.PolicyUserAdded>,
        IHandle<PolicyUserMsgs.RoleAdded>,
        IHandle<PolicyUserMsgs.RoleRemoved>,
        IHandle<PolicyUserMsgs.UserDeactivated>,
        IHandle<PolicyUserMsgs.UserReactivated>
    {
        public IConnectableCache<PolicyDTO, Guid> Policies => _policies;
        private readonly SourceCache<PolicyDTO, Guid> _policies = new SourceCache<PolicyDTO, Guid>(x => x.PolicyId);
        private readonly Dictionary<Guid, ApplicationDTO> _appById = new Dictionary<Guid, ApplicationDTO>();
        private readonly Dictionary<string, ApplicationDTO> _appByName = new Dictionary<string, ApplicationDTO>();
        private readonly Dictionary<Guid, PolicyUserDTO> _policyUsers = new Dictionary<Guid, PolicyUserDTO>();
        private readonly Dictionary<Guid, RoleDTO> _roles = new Dictionary<Guid, RoleDTO>();


        public ApplicationRm(IConfiguredConnection conn)
           : base(nameof(ApplicationRm), () => conn.GetListener(nameof(ApplicationRm)))
        {
            //set handlers
            EventStream.Subscribe<ApplicationMsgs.ApplicationCreated>(this);
            EventStream.Subscribe<ApplicationMsgs.PolicyCreated>(this);
            EventStream.Subscribe<ApplicationMsgs.RoleCreated>(this);
            EventStream.Subscribe<PolicyUserMsgs.PolicyUserAdded>(this);
            EventStream.Subscribe<PolicyUserMsgs.RoleAdded>(this);
            EventStream.Subscribe<PolicyUserMsgs.RoleRemoved>(this);
            EventStream.Subscribe<PolicyUserMsgs.UserDeactivated>(this);
            EventStream.Subscribe<PolicyUserMsgs.UserReactivated>(this);
            //read
            long? appCheckpoint;
            long? userCheckpoint;
            using (var reader = conn.GetReader(nameof(FilteredPoliciesRM), Handle))
            {
                reader.Read<Domain.SecuredApplication>(() => Idle);
                appCheckpoint = reader.Position;
                reader.Read<Domain.PolicyUser>(() => Idle);
                userCheckpoint = reader.Position;
            }
            //subscribe
            Start<Domain.SecuredApplication>(appCheckpoint);
            Start<Domain.PolicyUser>(userCheckpoint);
        }



        /// <summary>
        /// Gets the application with the specified ID.
        /// </summary>
        /// <param name="applicationId">The application ID.</param>
        /// <exception cref="KeyNotFoundException">Thrown if no application with that ID is found.</exception>
        public ApplicationDTO GetApplication(Guid applicationId) => _appById[applicationId];

        /// <summary>
        /// Gets the application with the specified Name.
        /// </summary>
        /// <param name="appName">The application name.</param>
        /// <exception cref="KeyNotFoundException">Thrown if no application with that Name is found.</exception>
        public ApplicationDTO GetApplication(string appName) => _appByName[appName];
        /// <summary>
        /// Gets the policies for the application with the specified Name.
        /// </summary>
        /// <param name="appName">The application name.</param>
        /// <exception cref="KeyNotFoundException">Thrown if no application with that Name is found.</exception>
        public IReadOnlyList<PolicyDTO> GetPolicies(string appName)
        {
            var app = _appByName[appName];
            return _policies.Items.Where(p => p.ApplicationId == app.ApplicationId).ToList();
        }
        public PolicyUserDTO GetPolicyUserByuserId(Guid userId)
        {
            return _policyUsers.Values.FirstOrDefault(u => u.UserId == userId);
        }
        /// <summary>
        /// Gets whether there is already a secured application with the given name and version.
        /// </summary>
        /// <param name="appName">The name of the application.</param>
        /// <param name="securityModelVersion">The version of the application's security model.</param>
        /// <returns>True if an application exists that matches both name and version, otherwise false.</returns>
        public bool ApplicationExists(string appName, Version securityModelVersion) => _appByName.Values.Any(x => x.Name == appName && x.SecurityModelVersion == securityModelVersion);

        public void Handle(ApplicationMsgs.ApplicationCreated @event)
        {
            if (_appById.ContainsKey(@event.ApplicationId)) { return; }
            var app = new ApplicationDTO(@event);
            _appById.Add(@event.ApplicationId, app);
            if (_appByName.ContainsKey(app.Name)) { _appByName[app.Name] = app; } //only keep most recent
            else { _appByName.Add(app.Name, app); }
        }

        public void Handle(ApplicationMsgs.PolicyCreated @event)
        {
            if (_appById.ContainsKey(@event.ApplicationId)) //in filtered list
            {
                if (_policies.Keys.Contains(@event.PolicyId)) { return; }
                _policies.AddOrUpdate(new PolicyDTO(@event));
            }
        }
        public void Handle(ApplicationMsgs.RoleCreated @event)
        {
            var policy = _policies.Lookup(@event.PolicyId);
            if (policy.HasValue && !_roles.ContainsKey(@event.RoleId))
            {
                var role = new RoleDTO(@event);
                _roles.Add(@event.RoleId, role);
                policy.Value.Roles.AddOrUpdate(role);
            }
        }

        public void Handle(PolicyUserMsgs.PolicyUserAdded @event)
        {
            var policy = _policies.Lookup(@event.PolicyId);
            if (policy.HasValue && !_policyUsers.ContainsKey(@event.PolicyUserId))
            {
                var policyUser = new PolicyUserDTO(@event);
                _policyUsers.Add(@event.PolicyUserId, policyUser);
                policy.Value.Users.AddOrUpdate(policyUser);
            }
        }

        public void Handle(PolicyUserMsgs.RoleAdded @event)
        {
            if (_policyUsers.TryGetValue(@event.PolicyUserId, out var user) &&
                _roles.TryGetValue(@event.RoleId, out var role))
            {
                if (user.RolesCache.Keys.Contains(@event.RoleId)) { return; }
                user.AddRole(role);
            }
        }

        public void Handle(PolicyUserMsgs.RoleRemoved @event)
        {
            if (_policyUsers.TryGetValue(@event.PolicyUserId, out var user))
            {
                user.RemoveRole(@event.RoleId);
            }
        }
        public void Handle(PolicyUserMsgs.UserDeactivated @event)
        {

            if (_policyUsers.TryGetValue(@event.PolicyUserId, out var user))
            {
                var policy = _policies.Lookup(user.PolicyId);
                if (policy.HasValue)
                {
                    policy.Value.Users.Remove(user);
                }
            }
        }

        public void Handle(PolicyUserMsgs.UserReactivated @event)
        {
            if (_policyUsers.TryGetValue(@event.PolicyUserId, out var user))
            {
                var policy = _policies.Lookup(user.PolicyId);
                if (policy.HasValue)
                {
                    policy.Value.Users.AddOrUpdate(user);
                }
            }
        }

    }
}
