using System;
using System.Collections.Generic;
using System.Linq;
using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Users.Domain.Aggregates;
using ReactiveDomain.Users.Messages;
using ReactiveDomain.Users.PolicyModel;
using User = ReactiveDomain.Users.PolicyModel.User;

namespace ReactiveDomain.Users.ReadModels
{
    //todo: we likely need a version of this that loads just a single application by name and version
    // for use in applications enforcing security policy
    /// <summary>
    /// A read model that contains a list of existing applications. 
    /// </summary>
    public class ApplicationsRM :
        ReadModelBase,
        IHandle<ApplicationMsgs.ApplicationCreated>,
        IHandle<RoleMsgs.RoleCreated>,
        IHandle<RoleMsgs.RoleMigrated>,
        IHandle<PolicyUserMsgs.UserCreated>,
        IHandle<PolicyUserMsgs.Deactivated>,
        IHandle<PolicyUserMsgs.Activated>,
        IHandle<UserPolicyMsgs.RoleAdded>,
        IHandle<PolicyUserMsgs.AuthDomainUpdated>,
        IHandle<PolicyUserMsgs.UserNameUpdated>,
        IHandle<UserPolicyMsgs.RoleRemoved>,
        IUserEntitlementRM
    {
        public List<User> ActivatedUsers => _users.Values.Where(x => x.IsActivated).ToList(); //todo: is this the best way?

        private readonly Dictionary<Guid, Role> _roles = new Dictionary<Guid, Role>();
        private readonly Dictionary<Guid, SecuredApplication> _applications = new Dictionary<Guid, SecuredApplication>();
        private readonly Dictionary<Guid, User> _users = new Dictionary<Guid, User>();

        /// <summary>
        /// Create a read model for getting information about existing applications.
        /// </summary>
        public ApplicationsRM(IConfiguredConnection conn)
            : base(nameof(ApplicationsRM), () => conn.GetListener(nameof(ApplicationsRM)))
        {
            //set handlers
            EventStream.Subscribe<ApplicationMsgs.ApplicationCreated>(this);
            EventStream.Subscribe<RoleMsgs.RoleCreated>(this);
            EventStream.Subscribe<RoleMsgs.RoleMigrated>(this);

            //read
            long? checkpoint;
            using (var reader = conn.GetReader(nameof(ApplicationsRM), this))
            {                
                reader.Read<SecuredApplicationAgg>();                
                checkpoint = reader.Position;
            }            
            //subscribe
            Start<SecuredApplicationAgg>(checkpoint);
        
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

            if (_applications.ContainsKey(@event.ApplicationId)) return;

            _applications.Add(
                @event.ApplicationId,
                new SecuredApplication(
                    @event.ApplicationId,
                    @event.Name,
                    @event.ApplicationVersion
                ));
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
                    @event.PolicyId));
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

        /// <summary>
        /// Handle a UserMsgs.UserCreated event.
        /// </summary>
        public void Handle(PolicyUserMsgs.UserCreated @event)
        {
            if (_users.ContainsKey(@event.Id)) return;
            _users.Add(
                @event.Id,
                new User(
                    @event.Id,
                    @event.UserName,
                    @event.SubjectId,
                    @event.AuthDomain));
        }
        public void Handle(PolicyUserMsgs.Deactivated @event)
        {
            if (!_users.ContainsKey(@event.UserId)) return;
            _users[@event.UserId].IsActivated = false;
        }

        public void Handle(PolicyUserMsgs.Activated @event)
        {
            if (!_users.ContainsKey(@event.UserId)) return;
            _users[@event.UserId].IsActivated = true;
        }

        public void Handle(UserPolicyMsgs.RoleAdded @event)
        {
            if (!_users.ContainsKey(@event.UserId) || !_roles.ContainsKey(@event.RoleId)) return;
            var role = _users[@event.UserId].Roles.FirstOrDefault(r => r.RoleId == @event.RoleId);
            if (role != null) return;
            _users[@event.UserId].Roles.Add(_roles[@event.RoleId]);
        }

        public void Handle(UserPolicyMsgs.RoleRemoved @event)
        {
            if (!_users.ContainsKey(@event.UserId) || !_roles.ContainsKey(@event.RoleId)) return;
            var role = _users[@event.UserId].Roles.FirstOrDefault(r => r.RoleId == @event.RoleId);
            if (role == null) return;
            _users[@event.UserId].Roles.Remove(role);
        }
        public void Handle(PolicyUserMsgs.AuthDomainUpdated @event)
        {
            if (!_users.ContainsKey(@event.UserId)) return;
            _users[@event.UserId].AuthDomain = @event.AuthDomain;
        }
        public void Handle(PolicyUserMsgs.UserNameUpdated @event)
        {
            if (!_users.ContainsKey(@event.UserId)) return;
            _users[@event.UserId].UserName = @event.UserName;
        }
        public List<Role> RolesForUser(
            string subjectId,
            string userName,
            string authDomain,
            Guid policyId)
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

            return user.Roles.Where(x => x.PolicyId == policyId).ToList();
        }

    }
}
