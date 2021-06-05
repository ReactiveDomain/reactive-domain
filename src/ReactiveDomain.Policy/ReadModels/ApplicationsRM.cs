using System;
using System.Collections.Generic;
using System.Linq;
using DynamicData;
using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Policy.Application;
using ReactiveDomain.Policy.Messages;
using ReactiveDomain.Users;
using ReactiveDomain.Users.ReadModels;


namespace ReactiveDomain.Policy.ReadModels
{
    //todo: we likely need a version of this that loads just a single application by name and version
    // for use in applications enforcing security policy
    /// <summary>
    /// A read model that contains a list of existing applications. 
    /// </summary>
    public class ApplicationsRM :
        ReadModelBase,
        IHandle<ApplicationMsgs.ApplicationCreated>,
        IHandle<ApplicationMsgs.STSClientDetailsAdded>,
        IHandle<ApplicationMsgs.STSClientSecretAdded>,
        IHandle<RoleMsgs.RoleCreated>//,
       
        //IHandle<UserPolicyMsgs.RoleAdded>,
       
        //IHandle<UserPolicyMsgs.RoleRemoved>
        //IUserEntitlementRM
    {
        public IConnectableCache<SecuredApplication,Guid> RegisteredApplications => _registeredApplications;
        private readonly SourceCache<SecuredApplication, Guid> _registeredApplications = new SourceCache<SecuredApplication, Guid>(x => x.Id);

        public IConnectableCache<Role, Guid> ApplicationRoles => _applicationRoles;
        private readonly SourceCache<Role, Guid> _applicationRoles = new SourceCache<Role, Guid>(x => x.RoleId);

        public List<UserDTO> ActivatedUsers => _users.Values.Where(x => x.Active).ToList(); //todo: is this the best way?

        private readonly Dictionary<Guid, Role> _roles = new Dictionary<Guid, Role>();
        private readonly Dictionary<Guid, SecuredApplication> _applications = new Dictionary<Guid, SecuredApplication>();
        private readonly Dictionary<Guid, UserDTO> _users = new Dictionary<Guid, UserDTO>();

        /// <summary>
        /// Create a read model for getting information about existing applications.
        /// </summary>
        public ApplicationsRM(IConfiguredConnection conn)
            : base(nameof(ApplicationsRM), () => conn.GetListener(nameof(ApplicationsRM)))
        {
            //set handlers
            EventStream.Subscribe<ApplicationMsgs.ApplicationCreated>(this);
            EventStream.Subscribe<ApplicationMsgs.STSClientSecretAdded>(this);
            EventStream.Subscribe<ApplicationMsgs.STSClientDetailsAdded>(this);
            EventStream.Subscribe<RoleMsgs.RoleCreated>(this);

            //read
            long? checkpoint;
            using (var reader = conn.GetReader(nameof(ApplicationsRM), this)) {
                reader.EventStream.Subscribe<Message>(this);
                reader.Read<Domain.SecuredApplication>();                
                checkpoint = reader.Position;
            }            
            //subscribe
            Start<Domain.SecuredApplication>(checkpoint);
        
            //todo: subscribe to user stream
        }

        /// <summary>
        /// Checks whether the specified application is known to the system.
        /// </summary>
        /// <param name="id">The unique ID of the application.</param>
        /// <returns>True if the user exists.</returns>
        public bool ApplicationExists(Guid id)
        {
            return _applications.ContainsKey(id);
        }

        /// <summary>
        /// Checks whether the specified application is known to the system.
        /// </summary>
        /// <param name="name">The application name.</param>
        /// <returns></returns>
        public bool ApplicationExists(string name)
        {
            return _applications.Values.Any(x => x.Name == name);
        }

        /// <summary>
        /// Gets the unique ID of the specified application.
        /// </summary>
        /// <param name="name">The application name.</param>
        /// <returns>Application guid if a application with matching properties was found, otherwise empty guid.</returns>
        public Guid GetApplicationId(string name)
        {
            var app = _applications.Values.FirstOrDefault(x => x.Name == name);
            return app?.Id ?? Guid.Empty;
        }

        /// <summary>
        /// Given the name of the role and the application, returns whether the role exists or not.
        /// </summary>
        public bool RoleExists(
            string name,
            Guid applicationId)
        {
            return TryGetRoleId(name, applicationId, out _);
        }

        /// <summary>
        /// Gets the unique ID of the specified role.
        /// </summary>
        /// <param name="name">The of the role.</param>
        /// <param name="applicationId">The application for which this role is defined.</param>        
        /// <param name="id">The unique ID of the role. This is the out parameter</param>
        /// <returns>True if a role with matching properties was found, otherwise false.</returns>
        public bool TryGetRoleId(
            string name,
            Guid applicationId,
            out Guid? id)
        {
            id = null;
            var role = _roles.Values.FirstOrDefault(r => r.PolicyId == applicationId && string.CompareOrdinal(r.Name, name) == 0);
            if (role != null)
                id = role.RoleId;
            return id != null;
        }


        /// <summary>
        /// Given the application created event, adds a new application to the collection of roles.
        /// </summary>
        public void Handle(ApplicationMsgs.ApplicationCreated @event)
        {

            if (_applications.ContainsKey(@event.ApplicationId)) { return; }
            var app = new SecuredApplication(
                    @event.ApplicationId,
                    @event.Name,
                    @event.ApplicationVersion,
                    @event.OneRolePerUser
                );
            _applications.Add(@event.ApplicationId, app);
            _registeredApplications.AddOrUpdate(app);
        }
        public void Handle(ApplicationMsgs.STSClientDetailsAdded @event) {
            if (_applications.TryGetValue(@event.ApplicationId, out var app)) {
                app.ClientSecret = @event.EncryptedClientSecret;
                app.RedirectionUris = @event.RedirectUris;
            }
        }

        public void Handle(ApplicationMsgs.STSClientSecretAdded @event) {
            if (_applications.TryGetValue(@event.ApplicationId, out var app)) {
                app.ClientSecret = @event.EncryptedClientSecret;
            }
        }
        /// <summary>
        /// Given the role created event, adds a new role to the collection of roles.
        /// </summary>
        public void Handle(RoleMsgs.RoleCreated @event)
        {
            if (_roles.ContainsKey(@event.RoleId)) return;

            var role = new Role(
                            @event.RoleId,
                            @event.Name,
                            @event.PolicyId);
            _roles.Add(
                @event.RoleId,
                role);

            _applicationRoles.AddOrUpdate(role);
        }
        
        //public void Handle(UserPolicyMsgs.RoleAdded @event)
        //{
        //    if (!_users.ContainsKey(@event.UserId) || !_roles.ContainsKey(@event.RoleId)) return;
        //    var role = _users[@event.UserId].Roles.FirstOrDefault(r => r.RoleId == @event.RoleId);
        //    if (role != null) return;
        //    _users[@event.UserId].Roles.Add(_roles[@event.RoleId]);
        //}

        //public void Handle(UserPolicyMsgs.RoleRemoved @event)
        //{
        //    if (!_users.ContainsKey(@event.UserId) || !_roles.ContainsKey(@event.RoleId)) return;
        //    var role = _users[@event.UserId].Roles.FirstOrDefault(r => r.RoleId == @event.RoleId);
        //    if (role == null) return;
        //    _users[@event.UserId].Roles.Remove(role);
        //}
       
        public List<Role> RolesForUser(
            string subjectId,
            string userName,
            string authDomain,
            Guid policyId)
        {
            throw new NotImplementedException("Roles are on the Security Policy not the user");
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
            if (!user.Active)
            {
                throw new UserDeactivatedException("UserId = " + subjectId + ", authDomain = " + authDomain);
            }

            //return user.Roles.Where(x => x.PolicyId == policyId).ToList();
        }

       
    }
}
