using System;
using System.Collections.Generic;
using System.Linq;
using Elbe.Domain;
using Elbe.Messages;
using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging.Bus;

namespace ReactiveDomain.Users.Identity.UserRolesData
{
    /// <summary>
    /// A read model that contains a list of users and their respective roles.
    /// </summary>
    public class UserEntitlementRM :
        ReadModelBase,
        IHandle<UserMsgs.UserCreated>,
        IHandle<RoleMsgs.RoleCreated>,
        IHandle<RoleMsgs.RoleRemoved>,
        IHandle<UserMsgs.Deactivated>,
        IHandle<UserMsgs.Activated>,
        IHandle<UserMsgs.RoleAssigned>,
        IHandle<UserMsgs.AuthDomainUpdated>,
        IHandle<UserMsgs.UserNameUpdated>,
        IHandle<UserMsgs.RoleUnassigned>, IUserEntitlementRM
    {
        public List<UserModel> ActivatedUsers => Users.FindAll(x => x.IsActivated);

        private List<UserModel> Users { get; } = new List<UserModel>();
        private List<RoleModel> Roles { get; } = new List<RoleModel>();
        private string _userStream => new PrefixedCamelCaseStreamNameBuilder("pki_elbe").GenerateForCategory(typeof(User));
        private string _roleStream => new PrefixedCamelCaseStreamNameBuilder("pki_elbe").GenerateForCategory(typeof(Role));

        /// <summary>
        /// Create a read model that contains a list of users and their respective roles.
        /// </summary>
        public UserEntitlementRM(Func<IListener> getListener)
            : base(nameof(UserEntitlementRM), getListener)
        {
            // ReSharper disable once RedundantTypeArgumentsOfMethod
            EventStream.Subscribe<UserMsgs.UserCreated>(this);
            EventStream.Subscribe<RoleMsgs.RoleCreated>(this);
            EventStream.Subscribe<RoleMsgs.RoleRemoved>(this);
            EventStream.Subscribe<UserMsgs.Deactivated>(this);
            EventStream.Subscribe<UserMsgs.Activated>(this);
            EventStream.Subscribe<UserMsgs.RoleAssigned>(this);
            EventStream.Subscribe<UserMsgs.RoleUnassigned>(this);
            EventStream.Subscribe<UserMsgs.AuthDomainUpdated>(this);
            EventStream.Subscribe<UserMsgs.UserNameUpdated>(this);
            Start(_roleStream, blockUntilLive: true);
            Start(_userStream, blockUntilLive: true);
        }

        public List<RoleModel> RolesForUser(
            string userSidFromAuthProvider,
            string userName,
            string authDomain,
            string application)
        {
            var user = Users.FirstOrDefault(x =>
                x.AuthDomain.Equals(authDomain, StringComparison.CurrentCultureIgnoreCase)
                && (string.IsNullOrEmpty(userSidFromAuthProvider) ||
                    string.IsNullOrEmpty(x.UserSidFromAuthProvider)
                    ? x.UserName.Equals(userName, StringComparison.CurrentCultureIgnoreCase)
                    : x.UserSidFromAuthProvider.Equals(userSidFromAuthProvider, StringComparison.CurrentCultureIgnoreCase))

            );
            if (user == null)
            {
                throw new UserNotFoundException("UserSidFromAuthProvider = " + userSidFromAuthProvider + ", authDomain = " + authDomain);
            }
            if (!user.IsActivated)
            {
                throw new UserDeactivatedException("UserSidFromAuthProvider = " + userSidFromAuthProvider + ", authDomain = " + authDomain);
            }
            return user.Roles.FindAll(x => x.Application == application);
        }
        public class RoleModel
        {
            public Guid RoleId { get; }
            public string Name { get; }
            public string Application { get; }

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

        public class UserModel
        {
            public Guid UserId { get; }
            public string UserName { get; set; }
            public string UserSidFromAuthProvider { get; set; }
            public string AuthDomain { get; set; }
            public List<RoleModel> Roles { get; } = new List<RoleModel>();
            public bool IsActivated { get; set; } = true;


            public UserModel(
                    Guid userId,
                    string userName,
                    string userSidFromAuthProvider,
                    string authDomain)
            {
                UserId = userId;
                UserName = userName;
                UserSidFromAuthProvider = userSidFromAuthProvider;
                AuthDomain = authDomain;
            }
        }

        /// <summary>
        /// Handle a UserMsgs.UserCreated event.
        /// </summary>
        public void Handle(UserMsgs.UserCreated message)
        {
            Users.Add(new UserModel(
                            message.Id,
                            message.UserName,
                            message.SubjectId,
                            message.AuthDomain));
        }

        public void Handle(RoleMsgs.RoleCreated message)
        {
            Roles.Add(new RoleModel(
                            message.RoleId,
                            message.Name,
                            message.Application));
        }

        public void Handle(RoleMsgs.RoleRemoved message)
        {
            var role = Roles.First(x => x.RoleId == message.RoleId);
            Roles.Remove(role);
            var usersWithRoleAssigned = Users.FindAll(x => x.Roles.Exists(y => y.RoleId == message.RoleId));
            foreach (var user in usersWithRoleAssigned)
            {
                user.Roles.Remove(role);
            }
        }

        public void Handle(UserMsgs.Deactivated message)
        {
            var user = Users.FirstOrDefault(x => x.UserId == message.UserId);
            if (user == null) return;
            user.IsActivated = false;
        }

        public void Handle(UserMsgs.Activated message)
        {
            var user = Users.FirstOrDefault(x => x.UserId == message.UserId);
            if (user == null) return;
            user.IsActivated = true;
        }

        public void Handle(UserMsgs.RoleAssigned message)
        {
            var user = Users.FirstOrDefault(x => x.UserId == message.UserId);
            var role = Roles.FirstOrDefault(x => x.RoleId == message.RoleId);
            if (user == null || role == null) return;
            user.Roles.Add(role);
        }

        public void Handle(UserMsgs.RoleUnassigned message)
        {
            var user = Users.First(x => x.UserId == message.UserId);
            var role = Roles.First(x => x.RoleId == message.RoleId);
            if (user == null || role == null) return;
            user.Roles.Remove(role);
        }
        public void Handle(UserMsgs.AuthDomainUpdated message)
        {
            var user = Users.FirstOrDefault(x => x.UserId == message.UserId);
            if (user != null)
            {
                user.AuthDomain = message.AuthDomain;
            }
        }
        public void Handle(UserMsgs.UserNameUpdated message)
        {
            var user = Users.FirstOrDefault(x => x.UserId == message.UserId);
            if (user != null)
            {
                user.UserName = message.UserName;
            }
        }
    }
}
