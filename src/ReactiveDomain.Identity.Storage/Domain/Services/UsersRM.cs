using System;
using System.Collections.Generic;
using System.Linq;
using Elbe.Messages;
using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging.Bus;
using Splat;

namespace Elbe.Domain
{
    /// <summary>
    /// A read model that contains a list of existing users. 
    /// </summary>
    public class UsersRM :
        ReadModelBase,
        IHandle<UserMsgs.UserCreated>,
        IHandle<UserMsgs.UserMigrated>,
        IHandle<UserMsgs.UserNameUpdated>,
        IHandle<UserMsgs.UserSidFromAuthProviderUpdated>
    {
        private Dictionary<Guid, UserModel> Users { get; } = new Dictionary<Guid, UserModel>();
        private readonly Dictionary<AuthDomain, HashSet<UserModel>> _userLookup = new Dictionary<AuthDomain, HashSet<UserModel>>();


        /// <summary>
        /// Create a read model for getting information about existing users.
        /// </summary>
        public UsersRM()
            : base(
                nameof(UsersRM),
                () => Locator.Current.GetService<Func<string, IListener>>().Invoke(nameof(UsersRM)))
        {
            EventStream.Subscribe<UserMsgs.UserCreated>(this);
            EventStream.Subscribe<UserMsgs.UserMigrated>(this);
            EventStream.Subscribe<UserMsgs.UserNameUpdated>(this);
            EventStream.Subscribe<UserMsgs.UserSidFromAuthProviderUpdated>(this);
            Start<User>(blockUntilLive: true);
        }

        /// <summary>
        /// Handle a UserMsgs.UserCreated event.
        /// </summary>
        public void Handle(UserMsgs.UserCreated message)
        {
            var model = new UserModel(
                message.Id,
                new AuthDomain(message.AuthProvider, message.AuthDomain),
                message.UserName,
                message.UserSidFromAuthProvider);

            AddUser(model);
        }

        /// <summary>
        /// Handle a UserMsgs.UserCreated event.
        /// </summary>
        public void Handle(UserMsgs.UserMigrated message)
        {
            var model = new UserModel(
                message.Id,
                new AuthDomain(message.AuthProvider, message.AuthDomain),
                message.UserName,
                message.UserSidFromAuthProvider);

            AddUser(model);
        }
        private void AddUser(UserModel model)
        {
            if (Users.TryGetValue(model.Id, out _))
            {
                Users[model.Id] = model;
            }
            else
            {
                Users.Add(model.Id, model);
            }

            if (!_userLookup.TryGetValue(model.Domain, out var domainUsers))
            {
                domainUsers = new HashSet<UserModel>();
                _userLookup.Add(model.Domain, domainUsers);
            }
            domainUsers.Add(model);
        }
        /// <summary>
        /// Handle a UserMsgs.UserNameUpdated event.
        /// </summary>
        public void Handle(UserMsgs.UserNameUpdated message)
        {
            if (!Users.TryGetValue(message.UserId, out var model)) { return; } //todo: what cases could this happen in?
            model.UserName = message.UserName;
        }
        /// <summary>
        /// Handle a UserMsgs.UserCreated event.
        /// </summary>
        public void Handle(UserMsgs.UserSidFromAuthProviderUpdated message)
        {
            if (!Users.TryGetValue(message.UserId, out var model)) { return; } //todo: what cases could this happen in?
            model.UserName = message.UserSidFromAuthProvider;
        }

        /// <summary>
        /// Checks whether the specified user is known to the system.
        /// </summary>
        /// <param name="id">The unique ID of the user.</param>
        /// <returns>True if the user exists.</returns>
        public bool UserExists(Guid id)
        {
            return Users.ContainsKey(id);
        }

        /// <summary>
        /// Checks whether the specified user is known to the system.
        /// </summary>
        /// <param name="authProvider">The identity provider.</param>
        /// <param name="authDomain">The user's domain.</param>
        /// <param name="userName">The user's unique username.</param>
        /// <param name="userSidFromAuthProvider">The user's unique userSidFromAuthProvider.</param>
        /// <returns></returns>
        public bool UserExists(
            string authProvider,
            string authDomain,
            string userName,
            string userSidFromAuthProvider)
        {
            return TryGetUserId(authProvider, authDomain, userName, userSidFromAuthProvider, out _);
        }

        /// <summary>
        /// Gets the unique ID of the specified user.
        /// </summary>
        /// <param name="authProvider">The identity provider.</param>
        /// <param name="authDomain">The user's domain.</param>
        /// <param name="userName">The user's unique username.</param>
        /// <param name="userSidFromAuthProvider">The user's unique userSidFromAuthProvider.</param>
        /// <param name="id">The unique ID of the user.</param>
        /// <returns>True if a user with matching properties was found, otherwise false.</returns>
        public bool TryGetUserId(
            string authProvider,
            string authDomain,
            string userName,
            string userSidFromAuthProvider,
            out Guid id)
        {
            id = Guid.Empty;
            try
            {
                var domain = new AuthDomain(authProvider, authDomain);
                if (!_userLookup.TryGetValue(domain, out var users))
                {
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(userSidFromAuthProvider))
                {
                    var user = users.FirstOrDefault(
                            usr => string.Equals(usr.UserSidFromAuthProvider, userSidFromAuthProvider,
                        StringComparison.OrdinalIgnoreCase));
                    if (user == null)
                    {
                        return false;
                    }
                    id = user.Id;
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(userName))
                {
                    var name = userName.ToUpper();
                    var user = users.FirstOrDefault(
                        usr => string.Equals(usr.UserName, name, StringComparison.OrdinalIgnoreCase));
                    if (user == null)
                    {
                        return false;
                    }
                    id = user.Id;
                    return true;
                }

                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public class UserModel
        {
            private string _userName;
            public Guid Id { get; }
            public AuthDomain Domain { get; }

            public string UserName
            {
                get => _userName;
                set => _userName = value.ToUpper();
            }

            public string UserSidFromAuthProvider { get; set; }

            public UserModel(
                Guid id,
                AuthDomain domain,
                string userName,
                string userSidFromAuthProvider)
            {
                Id = id;
                Domain = domain;
                UserName = userName;
                UserSidFromAuthProvider = userSidFromAuthProvider;
            }
        }

        public readonly struct AuthDomain
        {
            public string Provider { get; }
            public string Domain { get; }

            public AuthDomain(string provider, string domain)
            {
                Provider = provider.ToUpper();
                Domain = domain.ToUpper();
            }

            public override bool Equals(object obj)
            {
                if (!(obj is AuthDomain)) return false;
                AuthDomain other = (AuthDomain)obj;
                return Provider.Equals(other.Provider) && Domain.Equals(other.Domain);
            }

            public override int GetHashCode()
            {
                return ToString().GetHashCode();
            }

            public override string ToString()
            {
                return $"{Provider}:{Domain}";
            }
        }
    }
}
