using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Policy.Messages;
using ReactiveDomain.Policy.ReadModels;

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
        IHandle<RoleMsgs.RoleCreated>//,
                                     //IHandle<UserMsgs.UserCreated>,
                                     //IHandle<UserMsgs.Deactivated>,
                                     //IHandle<UserMsgs.Activated>,
                                     //IHandle<UserMsgs.RoleAssigned>,
                                     //IHandle<UserMsgs.AuthDomainUpdated>,
                                     //IHandle<UserMsgs.UserNameUpdated>,
                                     //IHandle<UserMsgs.RoleUnassigned>
    {
        //public data
        public SecurityPolicy Policy;

        private readonly Dictionary<Guid, Role> _roles = new Dictionary<Guid, Role>();
        private SecuredApplication dbApp;
        

        /// <summary>
        /// Create a read model for a synchronized Security Policy.
        /// </summary>
        public SecurityPolicySyncService(
            SecurityPolicy basePolicy,
            IConfiguredConnection conn)
            : base(nameof(ApplicationsRM), () => conn.GetListener(nameof(SecurityPolicySyncService)))
        {
            var repo = conn.GetCorrelatedRepository();
            ICorrelatedMessage source = new SyncServiceCorrelationSource { CorrelationId = Guid.NewGuid() };

            Policy = basePolicy ?? new SecurityPolicyBuilder().Build();
           
            var appList = new Dictionary<string, Guid>();
            using (var appReader = conn.GetReader("AppReader"))
            {
                appReader.EventStream.Subscribe(new AdHocHandler<ApplicationMsgs.ApplicationCreated>(evt => appList.Add($"{evt.Name}-{evt.ApplicationVersion}", evt.ApplicationId)));
                appReader.Read(typeof(ApplicationMsgs.ApplicationCreated));
            }
            Guid appId;
            if (appList.ContainsKey($"{Policy.ApplicationName}-{Policy.ApplicationVersion}"))
            {
                appId = appList[$"{Policy.ApplicationName}-{Policy.ApplicationVersion}"];
            }
            else
            {
                appId = Guid.NewGuid();
                var app = new Domain.SecuredApplication(appId, Policy.ApplicationName, Policy.ApplicationVersion, source);
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
            EventStream.Subscribe<RoleMsgs.RoleCreated>(this);

            using (var appReader = conn.GetReader("appReader")) {
                appReader.EventStream.Subscribe<Message>(this);
                appReader.Read<Domain.SecuredApplication>(appId);
            }
            Policy.OwningApplication.UpdateApplicationDetails(appId);
            Policy.OwningApplication.ClientSecret = dbApp.ClientSecret;
            Policy.OwningApplication.RedirectionUris = dbApp.RedirectionUris;
            Policy.PolicyId = dbApp.Policies.First().PolicyId; //todo:add multi policy support

            //enrich db with roles from the base policy, if any are missing
            foreach (var role in Policy.Roles)
            {
                if (_roles.Values.All(r => !r.Name.Equals(role.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    var roleId = Guid.NewGuid();
                    var application = repo.GetById<Domain.SecuredApplication>(appId, source);
                    application.DefaultPolicy.AddRole(roleId, role.Name);
                    repo.Save(application);
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

            //_subjectRM = new SubjectRM(() => conn.GetQueuedListener(nameof(SubjectRM)));

            //todo: subscribe to user assignments

            //todo: subscribe to user stream???
            //Start<ApplicationRoot>(appId, blockUntilLive: true);
        }

        public void Handle(ApplicationMsgs.ApplicationCreated @event)
        {
            if (dbApp != null) { return; }

            dbApp =
                new SecuredApplication(
                    @event.ApplicationId,
                    @event.Name,
                    @event.ApplicationVersion
                );
        }

        public void Handle(ApplicationMsgs.STSClientDetailsAdded @event) {
            if (dbApp.Id != @event.ApplicationId){ return; }
            dbApp.ClientSecret = @event.EncryptedClientSecret;
            dbApp.RedirectionUris = @event.RedirectUris;
        }

        public void Handle(ApplicationMsgs.STSClientSecretAdded @event) {
            if (dbApp.Id != @event.ApplicationId){ return; }
            dbApp.ClientSecret = @event.EncryptedClientSecret;
        }
        public void Handle(ApplicationMsgs.PolicyCreated @event)
        {
            if (dbApp.Id != @event.ApplicationId){ return; }
            dbApp.AddPolicy(
                new SecurityPolicy(
                    @event.ClientId,
                    @event.PolicyId, dbApp,
                    principal => default
                )
            );
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
        class SyncServiceCorrelationSource : ICorrelatedMessage
        {
            public Guid MsgId => Guid.NewGuid();
            public Guid CorrelationId { get; set; }
            public Guid CausationId { get; set; }
        }
    }
}
