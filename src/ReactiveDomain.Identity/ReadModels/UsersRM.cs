using System;
using System.Collections.Generic;
using System.Linq;
using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Users.Domain.Aggregates;
using ReactiveDomain.Users.Messages;
using ReactiveDomain.Util;

namespace ReactiveDomain.Users.ReadModels
{
    /// <summary>
    /// A read model that contains a list of existing users. 
    /// </summary>
    public class UsersRM :
        ReadModelBase,
        IHandle<PolicyUserMsgs.UserCreated>,
        IHandle<PolicyUserMsgs.UserMigrated>,
        IHandle<PolicyUserMsgs.UserNameUpdated>
    {
        private Dictionary<Guid, UserModel> Users { get; } = new Dictionary<Guid, UserModel>();
        private readonly Dictionary<AuthDomain, HashSet<UserModel>> _userLookup = new Dictionary<AuthDomain, HashSet<UserModel>>();


        /// <summary>
        /// Create a read model for getting information about existing users.
        /// </summary>
        public UsersRM(Func<IListener> getListener )
            : base(nameof(UsersRM),getListener)
        {
            EventStream.Subscribe<PolicyUserMsgs.UserCreated>(this);
            EventStream.Subscribe<PolicyUserMsgs.UserMigrated>(this);
            EventStream.Subscribe<PolicyUserMsgs.UserNameUpdated>(this);
            Start<Domain.Aggregates.IdentityAgg>(blockUntilLive: true);
        }

        /// <summary>
        /// Handle a UserMsgs.UserCreated event.
        /// </summary>
        public void Handle(PolicyUserMsgs.UserCreated message)
        {
            var model = new UserModel(
                message.Id,
                new AuthDomain(message.AuthProvider, message.AuthDomain),
                message.UserName,
                message.SubjectId);

            AddUser(model);
        }

        /// <summary>
        /// Handle a UserMsgs.UserCreated event.
        /// </summary>
        public void Handle(PolicyUserMsgs.UserMigrated message)
        {
            var model = new UserModel(
                message.Id,
                new AuthDomain(message.AuthProvider, message.AuthDomain),
                message.UserName,
                message.SubjectId);

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
        public void Handle(PolicyUserMsgs.UserNameUpdated message)
        {
            if (!Users.TryGetValue(message.UserId, out var model)) { return; } //todo: what cases could this happen in?
            model.UserName = message.UserName;
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
        /// <param name="subjectId">The user's unique userSidFromAuthProvider.</param>
        /// <returns></returns>
        public bool UserExists(
            string authProvider,
            string authDomain,
            string subjectId,
            out Guid userId)
        {
            return TryGetUserId(authProvider, authDomain,  subjectId, out userId);
        }

        /// <summary>
        /// Gets the unique ID of the specified user.
        /// </summary>
        /// <param name="authProvider">The identity provider.</param>
        /// <param name="authDomain">The user's domain.</param>
        /// <param name="subjectId">The user's unique userSidFromAuthProvider.</param>
        /// <param name="id">The unique ID of the user.</param>
        /// <returns>True if a user with matching properties was found, otherwise false.</returns>
        public bool TryGetUserId(
            string authProvider,
            string authDomain,
            string subjectId,
            out Guid id)
        {
            Ensure.NotNullOrEmpty(subjectId, nameof(subjectId));
            id = Guid.Empty;
            try
            {
                var domain = new AuthDomain(authProvider, authDomain);
                if (!_userLookup.TryGetValue(domain, out var users))
                {
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(subjectId))
                {
                    var user = users.FirstOrDefault(
                            usr => string.Equals(usr.SubjectId, subjectId,
                        StringComparison.OrdinalIgnoreCase));
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

            public string SubjectId { get; set; }

            public UserModel(
                Guid id,
                AuthDomain domain,
                string userName,
                string subjectId)
            {
                Id = id;
                Domain = domain;
                UserName = userName;
                SubjectId = subjectId;
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
