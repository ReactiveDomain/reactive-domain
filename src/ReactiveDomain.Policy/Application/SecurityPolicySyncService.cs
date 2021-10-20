﻿using System;
using System.Collections.Generic;
using System.Linq;
using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Policy.Messages;
using ReactiveDomain.Policy.ReadModels;
using ReactiveDomain.Users.ReadModels;

namespace ReactiveDomain.Policy.Application
{
    // for use in applications enforcing security policy
    /// <summary>
    /// A read model that contains a synchronized security policy for the application. 
    /// </summary>
    public class SecurityPolicySyncService :
        ReadModelBase,
        IHandle<ApplicationMsgs.ApplicationCreated>,
        IHandle<ApplicationMsgs.STSClientDetailsAdded>,
        IHandle<ApplicationMsgs.STSClientSecretAdded>,
        IHandle<ApplicationMsgs.PolicyCreated>,
        IHandle<ApplicationMsgs.RoleCreated>
    {
        //public data
        public SecurityPolicy Policy;

        private readonly Dictionary<Guid, Role> _roles = new Dictionary<Guid, Role>();
        private SecuredApplication _dbApp;


        /// <summary>
        /// Create a read model for a synchronized Security Policy.
        /// </summary>
        public SecurityPolicySyncService(
            SecurityPolicy basePolicy,
            IConfiguredConnection conn)
            : base(nameof(SecurityPolicySyncService), () => conn.GetListener(nameof(SecurityPolicySyncService)))
        {
            var repo = conn.GetCorrelatedRepository();
            ICorrelatedMessage source = new CorrelationSource { CorrelationId = Guid.NewGuid() };

            Policy = basePolicy ?? new SecurityPolicyBuilder().Build();

            var appList = new Dictionary<string, Guid>();
            using (var appReader = conn.GetReader("AppReader", evt => { if (evt is ApplicationMsgs.ApplicationCreated @event) { appList.Add($"{@event.Name}-{@event.SecurityModelVersion}", @event.ApplicationId); } }))
            {
                appReader.Read(typeof(ApplicationMsgs.ApplicationCreated), () => Idle);
            }
            Guid appId;
            if (appList.ContainsKey($"{Policy.ApplicationName}-{Policy.SecurityModelVersion}"))
            {
                appId = appList[$"{Policy.ApplicationName}-{Policy.SecurityModelVersion}"];
            }
            else
            {
                appId = Guid.NewGuid();
                var policyId = Policy.PolicyId == Guid.Empty ? Guid.NewGuid() : Policy.PolicyId;
                var app = new Domain.SecuredApplication(appId, policyId, Policy.ApplicationName, Policy.SecurityModelVersion, Policy.OneRolePerUser, source);
                //todo: encrypt this
                app.AddSTSClientSecret($"{Guid.NewGuid()}@PKI");
                repo.Save(app);
            }
            //build RM for targeted app

            EventStream.Subscribe<ApplicationMsgs.ApplicationCreated>(this);
            EventStream.Subscribe<ApplicationMsgs.STSClientSecretAdded>(this);
            EventStream.Subscribe<ApplicationMsgs.STSClientDetailsAdded>(this);
            EventStream.Subscribe<ApplicationMsgs.ApplicationCreated>(this);
            EventStream.Subscribe<ApplicationMsgs.PolicyCreated>(this);
            EventStream.Subscribe<ApplicationMsgs.RoleCreated>(this);

            using (var appReader = conn.GetReader("appReader", Handle))
            {
                appReader.Read<Domain.SecuredApplication>(appId, () => Idle);
            }
            Policy.OwningApplication.UpdateApplicationDetails(appId);
            Policy.OwningApplication.ClientSecret = _dbApp.ClientSecret;
            Policy.OwningApplication.RedirectionUris = _dbApp.RedirectionUris;
            Policy.PolicyId = _dbApp.Policies.First().PolicyId; //todo:add multi policy support

            //enrich db with roles from the base policy, if any are missing
            foreach (var role in Policy.Roles)
            {
                if (_roles.Values.All(r => !r.Name.Equals(role.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    var roleId = Guid.NewGuid();
                    var application = repo.GetById<Domain.SecuredApplication>(appId, source);
                    application.DefaultPolicy.AddRole(roleId, role.Name);
                    repo.Save(application);
                    role.SetRoleId(roleId, Policy.PolicyId);
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
                    baseRole.SetRoleId(role.RoleId, Policy.PolicyId);
                }
            }

            var policyUserRm = new PolicyUserRm(conn);
            var usersRm = new UsersRm(conn);
            if (policyUserRm.UsersByPolicy.ContainsKey(Policy.PolicyId))
            {
                var userIds = policyUserRm.UsersByPolicy[Policy.PolicyId];
                foreach (var id in userIds)
                {
                    policyUserRm.TryGetPolicyUserId(id, Policy.PolicyId, out var policyUserId); // Guid.Empty if none found
                    if (usersRm.UsersById.TryGetValue(id, out var user))
                    {
                        Policy.AddOrUpdateUser(Policy.GetPolicyUserFrom(policyUserId, user, conn, new List<string>()));
                    }
                }
            }
            //todo: update/replace policy users as users are added and role assignments change ??
            //Start<ApplicationRoot>(appId, blockUntilLive: true);
        }

        public void Handle(ApplicationMsgs.ApplicationCreated @event)
        {
            if (_dbApp != null) { return; }

            _dbApp =
                new SecuredApplication(
                    @event.ApplicationId,
                    @event.Name,
                    @event.SecurityModelVersion,
                    @event.OneRolePerUser
                );
        }

        public void Handle(ApplicationMsgs.STSClientDetailsAdded @event)
        {
            if (_dbApp.Id != @event.ApplicationId) { return; }
            _dbApp.ClientSecret = @event.EncryptedClientSecret;
            _dbApp.RedirectionUris = @event.RedirectUris;
        }

        public void Handle(ApplicationMsgs.STSClientSecretAdded @event)
        {
            if (_dbApp.Id != @event.ApplicationId) { return; }
            _dbApp.ClientSecret = @event.EncryptedClientSecret;
        }
        public void Handle(ApplicationMsgs.PolicyCreated @event)
        {
            if (_dbApp.Id != @event.ApplicationId) { return; }
            _dbApp.AddPolicy(
                new SecurityPolicy(
                    @event.ClientId,
                    @event.PolicyId, _dbApp
                )
            );
        }

        /// <summary>
        /// Given the role created event, adds a new role to the collection of roles.
        /// </summary>
        public void Handle(ApplicationMsgs.RoleCreated @event)
        {
            if (_roles.ContainsKey(@event.RoleId)) return;
            _roles.Add(
                @event.RoleId,
                new Role(
                    @event.RoleId,
                    @event.Name,
                    Policy.PolicyId));
        }
        public class CorrelationSource : ICorrelatedMessage
        {
            public Guid MsgId => Guid.NewGuid();
            public Guid CorrelationId { get; set; }
            public Guid CausationId { get; set; }
        }
    }
}
