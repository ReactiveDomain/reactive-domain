using System;
using ReactiveDomain.Messaging;

namespace ReactiveDomain.Identity.Storage.Messages
{
    /// <summary>
    /// Messages for the User domain.
    /// </summary>
    public class UserMsgs
    {
        /// <summary>
        /// Create a new user.
        /// </summary>
        public class CreateUser : Command
        {
            /// <summary>The unique ID of the new user.</summary>
            public readonly Guid Id;
            /// <summary>The unique ID from the auth provider (e.g. SID) of the authenticated user.</summary>
            public readonly string UserSidFromAuthProvider;
            /// <summary>The identity provider.</summary>
            public readonly string AuthProvider;
            /// <summary>The user's domain.</summary>
            public readonly string AuthDomain;
            /// <summary>The username, which should be unique within the <see cref="AuthDomain"/>.</summary>
            public readonly string UserName;
            /// <summary>The user's given name. This is the first name in most cultures.</summary>
            public readonly string GivenName;
            /// <summary>The user's surname or family name. This is the last name in most cultures.</summary>
            public readonly string Surname;
            /// <summary>The user's email address.</summary>
            public readonly string Email;
            /// <summary> User full name for local windows users</summary>
            public readonly string FullName;

            /// <summary>
            /// Create a new user.
            /// </summary>
            /// <param name="id">The unique ID of the new user.</param>
            /// <param name="userSidFromAuthProvider">The unique ID from the auth provider (e.g. SID) of the authenticated user.</param>
            /// <param name="authProvider">The identity provider.</param>
            /// <param name="authDomain">The user's domain.</param>
            /// <param name="userName">The username, which should be unique within the <see cref="AuthDomain"/>.</param>
            /// <param name="givenName">The user's given name. This is the first name in most cultures.</param>
            /// <param name="surname">The user's surname or family name. This is the last name in most cultures.</param>
            /// <param name="email">The user's email address.</param>
            /// <param name="fullName">user full name.</param>
            public CreateUser(
                Guid id,
                string userSidFromAuthProvider,
                string authProvider,
                string authDomain,
                string userName,
                string fullName,
                string givenName,
                string surname,
                string email
                )
            {
                Id = id;
                UserSidFromAuthProvider = userSidFromAuthProvider;
                AuthProvider = authProvider;
                AuthDomain = authDomain;
                UserName = userName;
                FullName = fullName;
                GivenName = givenName;
                Surname = surname;
                Email = email;

            }
        }

        /// <summary>
        /// A new user was created.
        /// </summary>
        public class UserCreated : Event
        {
            /// <summary>The unique ID of the new user.</summary>
            public readonly Guid Id;
            /// <summary>The unique ID from the auth provider (e.g. SID) of the authenticated user.</summary>
            public readonly string UserSidFromAuthProvider;
            /// <summary>The identity provider.</summary>
            public readonly string AuthProvider;
            /// <summary>The user's domain.</summary>
            public readonly string AuthDomain;
            /// <summary>The username, which should be unique within the <see cref="AuthDomain"/>.</summary>
            public readonly string UserName;
            /// <summary>The user's given name. This is the first name in most cultures.</summary>
            public readonly string GivenName;
            /// <summary>The user's surname or family name. This is the last name in most cultures.</summary>
            public readonly string Surname;
            /// <summary>The user's email address.</summary>
            public readonly string Email;
            /// <summary> User full name for local windows users</summary>
            public readonly string FullName;
            /// <summary>
            /// A new user was created.
            /// </summary>
            /// <param name="id">The unique ID of the new user.</param>
            ///<param name="userSidFromAuthProvider">The unique ID from the auth provider (e.g. SID) of the authenticated user.</param>
            /// <param name="authProvider">The identity provider.</param>
            /// <param name="authDomain">The user's domain.</param>
            /// <param name="userName">The username, which should be unique within the <see cref="AuthDomain"/>.</param>
            /// <param name="givenName">The user's given name. This is the first name in most cultures.</param>
            /// <param name="surname">The user's surname or family name. This is the last name in most cultures.</param>
            /// <param name="email">The user's email address.</param>
            /// <param name="fullName">user full name.</param>
            public UserCreated(
                Guid id,
                string userSidFromAuthProvider,
                string authProvider,
                string authDomain,
                string userName,
                string fullName,
                string givenName,
                string surname,
                string email
                )
            {
                Id = id;
                UserSidFromAuthProvider = userSidFromAuthProvider;
                AuthProvider = authProvider;
                AuthDomain = authDomain;
                UserName = userName;
                FullName = fullName;
                GivenName = givenName;
                Surname = surname;
                Email = email;

            }
        }
          /// <summary>
        /// User data was migrated.
        /// </summary>
        public class UserMigrated : Event
        {
            /// <summary>The unique ID of the user.</summary>
            public readonly Guid Id;
            /// <summary>The unique ID from the auth provider (e.g. SID) of the authenticated user.</summary>
            public readonly string UserSidFromAuthProvider;
            /// <summary>The identity provider.</summary>
            public readonly string AuthProvider;
            /// <summary>The user's domain.</summary>
            public readonly string AuthDomain;
            /// <summary>The username, which should be unique within the <see cref="AuthDomain"/>.</summary>
            public readonly string UserName;
            /// <summary>The user's given name. This is the first name in most cultures.</summary>
            public readonly string GivenName;
            /// <summary>The user's surname or family name. This is the last name in most cultures.</summary>
            public readonly string Surname;
            /// <summary>The user's email address.</summary>
            public readonly string Email;
            /// <summary> User full name for local windows users</summary>
            public readonly string FullName;
            /// <summary> The source stream the user was migrated from.</summary>
            public readonly string Source;
            /// <summary> The number of Events migrated.</summary>
            public readonly int EventCount;

            /// <summary>
            /// User data was migrated.
            /// </summary>
            /// <param name="id">The unique ID of the new user.</param>
            ///<param name="userSidFromAuthProvider">The unique ID from the auth provider (e.g. SID) of the authenticated user.</param>
            /// <param name="authProvider">The identity provider.</param>
            /// <param name="authDomain">The user's domain.</param>
            /// <param name="userName">The username, which should be unique within the <see cref="AuthDomain"/>.</param>
            /// <param name="givenName">The user's given name. This is the first name in most cultures.</param>
            /// <param name="surname">The user's surname or family name. This is the last name in most cultures.</param>
            /// <param name="email">The user's email address.</param>
            /// <param name="fullName">user full name.</param>
            ///<param name="source">The source stream the user was migrated from.</param>
            ///<param name="eventCount">The number of Events migrated.</param>
            public UserMigrated(
                Guid id,
                string userSidFromAuthProvider,
                string authProvider,
                string authDomain,
                string userName,
                string fullName,
                string givenName,
                string surname,
                string email,
                string source, 
                int eventCount)
            {
                Id = id;
                UserSidFromAuthProvider = userSidFromAuthProvider;
                AuthProvider = authProvider;
                AuthDomain = authDomain;
                UserName = userName;
                FullName = fullName;
                GivenName = givenName;
                Surname = surname;
                Email = email;
                Source = source;
                EventCount = eventCount;
            }
        }
        /// <summary>
        /// A user was successfully authenticated.
        /// </summary>
        public class Authenticated : Event
        {
            /// <summary>The ID of the authenticated user.</summary>
            public readonly Guid Id;
            /// <summary>The date and time in UTC on the Elbe Server when the authentication was logged.</summary>
            public readonly DateTime TimeStamp;
            /// <summary>The IP address of the host asking for authentication.</summary>
            public readonly string HostIPAddress;

            /// <summary>
            /// A user was successfully authenticated.
            /// </summary>
            /// <param name="id">The ID of the authenticated user.</param>
            /// <param name="timeStamp">The date and time in UTC on the Elbe Server when the authentication was logged.</param>
            /// <param name="hostIPAddress">The IP address of the host asking for authentication.</param>
            public Authenticated(
                Guid id,
                DateTime timeStamp,
                string hostIPAddress)
            {
                Id = id;
                TimeStamp = timeStamp;
                HostIPAddress = hostIPAddress;
            }
        }
        /// <summary>
        /// A user was not successfully authenticated.
        /// </summary>
        public class AuthenticationFailed : Event
        {
            /// <summary>The ID of the not authenticated user.</summary>
            public readonly Guid Id;
            /// <summary>The date and time in UTC on the Elbe Server when the authentication was logged.</summary>
            public readonly DateTime TimeStamp;
            /// <summary>The IP address of the host asking for authentication.</summary>
            public readonly string HostIPAddress;

            /// <summary>
            /// A user was successfully authenticated.
            /// </summary>
            /// <param name="id">The ID of the not authenticated user.</param>
            /// <param name="timeStamp">The date and time in UTC on the Elbe Server when the authentication attempt was logged.</param>
            /// <param name="hostIPAddress">The IP address of the host asking for authentication.</param>
            public AuthenticationFailed(
                Guid id,
                DateTime timeStamp,
                string hostIPAddress)
            {
                Id = id;
                TimeStamp = timeStamp;
                HostIPAddress = hostIPAddress;
            }
        }
        /// <summary>
        /// A user was not successfully authenticated because account is locked.
        /// </summary>
        public class AuthenticationFailedAccountLocked : Event
        {
            /// <summary>The ID of the not authenticated user.</summary>
            public readonly Guid Id;
            /// <summary>The date and time in UTC on the Elbe Server when the authentication was logged.</summary>
            public readonly DateTime TimeStamp;
            /// <summary>The IP address of the host asking for authentication.</summary>
            public readonly string HostIPAddress;

            /// <summary>
            /// A user was not successfully authenticated because account is locked.
            /// </summary>
            /// <param name="id">The ID of the not authenticated user.</param>
            /// <param name="timeStamp">The date and time in UTC on the Elbe Server when the authentication attempt was logged.</param>
            /// <param name="hostIPAddress">The IP address of the host asking for authentication.</param>
            public AuthenticationFailedAccountLocked(
                Guid id,
                DateTime timeStamp,
                string hostIPAddress)
            {
                Id = id;
                TimeStamp = timeStamp;
                HostIPAddress = hostIPAddress;
            }
        }

        /// <summary>
        /// A user was not successfully authenticated because account is disabled.
        /// </summary>
        public class AuthenticationFailedAccountDisabled : Event
        {
            /// <summary>The ID of the not authenticated user.</summary>
            public readonly Guid Id;
            /// <summary>The date and time in UTC on the Elbe Server when the authentication was logged.</summary>
            public readonly DateTime TimeStamp;
            /// <summary>The IP address of the host asking for authentication.</summary>
            public readonly string HostIPAddress;

            /// <summary>
            /// A user was not successfully authenticated because account is disabled.
            /// </summary>
            /// <param name="id">The ID of the not authenticated user.</param>
            /// <param name="timeStamp">The date and time in UTC on the Elbe Server when the authentication attempt was logged.</param>
            /// <param name="hostIPAddress">The IP address of the host asking for authentication.</param>
            public AuthenticationFailedAccountDisabled(
                Guid id,
                DateTime timeStamp,
                string hostIPAddress)
            {
                Id = id;
                TimeStamp = timeStamp;
                HostIPAddress = hostIPAddress;
            }
        }

        /// <summary>
        /// A user was not successfully authenticated because invalid credentials were supplied.
        /// </summary>
        public class AuthenticationFailedInvalidCredentials : Event
        {
            /// <summary>The ID of the not authenticated user.</summary>
            public readonly Guid Id;
            /// <summary>The date and time in UTC on the Elbe Server when the authentication was logged.</summary>
            public readonly DateTime TimeStamp;
            /// <summary>The IP address of the host asking for authentication.</summary>
            public readonly string HostIPAddress;

            /// <summary>
            /// A user was not successfully authenticated because invalid credentials were supplied.
            /// </summary>
            /// <param name="id">The ID of the not authenticated user.</param>
            /// <param name="timeStamp">The date and time in UTC on the Elbe Server when the authentication attempt was logged.</param>
            /// <param name="hostIPAddress">The IP address of the host asking for authentication.</param>
            public AuthenticationFailedInvalidCredentials(
                Guid id,
                DateTime timeStamp,
                string hostIPAddress)
            {
                Id = id;
                TimeStamp = timeStamp;
                HostIPAddress = hostIPAddress;
            }
        }

        /// <summary>
        /// Assign a role to a user.
        /// </summary>
        public class AssignRole : Command
        {
            /// <summary> The ID of the user.</summary>
            public readonly Guid UserId;
            /// <summary> The ID of the role.</summary>
            public readonly Guid RoleId;

            /// <summary>
            /// Assign a role to a user.
            /// </summary>
            public AssignRole(Guid userId, Guid roleId)
            {
                UserId = userId;
                RoleId = roleId;
            }
        }

        /// <summary>
        /// A role was assigned to a user.
        /// </summary>
        public class RoleAssigned : Event
        {
            /// <summary> The ID of the user.</summary>
            public readonly Guid UserId;
            /// <summary> The ID of the role.</summary>
            public readonly Guid RoleId;

            /// <summary>
            /// A role was assigned to a user.
            /// </summary>
            public RoleAssigned(Guid userId, Guid roleId)
            {
                UserId = userId;
                RoleId = roleId;
            }
        }

        /// <summary>
        /// Unassign a role.
        /// </summary>
        public class UnassignRole : Command
        {
            /// <summary> The ID of the user.</summary>
            public readonly Guid UserId;
            /// <summary> The ID of the role.</summary>
            public readonly Guid RoleId;

            /// <summary>
            /// Unassign a role.
            /// </summary>
            public UnassignRole(Guid userId, Guid roleId)
            {
                UserId = userId;
                RoleId = roleId;
            }
        }

        /// <summary>
        /// Role unassigned.
        /// </summary>
        public class RoleUnassigned : Event
        {
            /// <summary> The ID of the user.</summary>
            public readonly Guid UserId;
            /// <summary> The ID of the role.</summary>
            public readonly Guid RoleId;

            /// <summary>
            /// Role unassigned.
            /// </summary>
            public RoleUnassigned(Guid userId, Guid roleId)
            {
                UserId = userId;
                RoleId = roleId;
            }
        }

        /// <summary>
        /// Deactivate a user.
        /// </summary>
        public class Deactivate : Command
        {
            /// <summary> The ID of the user.</summary>
            public readonly Guid UserId;

            /// <summary>
            /// Deactivate a user.
            /// </summary>
            public Deactivate(Guid userId)
            {
                UserId = userId;
            }
        }

        /// <summary>
        /// User is deactivated.
        /// </summary>
        public class Deactivated : Event
        {
            /// <summary> The ID of the user.</summary>
            public readonly Guid UserId;

            /// <summary>
            /// User is deactivated.
            /// </summary>
            public Deactivated(Guid userId)
            {
                UserId = userId;
            }
        }

        /// <summary>
        /// Activate a user.
        /// </summary>
        public class Activate : Command
        {
            /// <summary> The ID of the user.</summary>
            public readonly Guid UserId;

            /// <summary>
            /// Activate a user.
            /// </summary>
            public Activate(Guid userId)
            {
                UserId = userId;
            }
        }

        /// <summary>
        /// User is Activated.
        /// </summary>
        public class Activated : Event
        {
            /// <summary> The ID of the user.</summary>
            public readonly Guid UserId;

            /// <summary>
            /// User is activated.
            /// </summary>
            public Activated(Guid userId)
            {
                UserId = userId;
            }
        }

        /// <summary>
        /// Update a user's given name.
        /// </summary>
        public class UpdateGivenName : Command
        {
            /// <summary>The unique ID of the user.</summary>
            public readonly Guid UserId;
            /// <summary>The user's given name. This is the first name in most cultures.</summary>
            public readonly string GivenName;

            /// <summary>
            /// Modify a user's given name.
            /// </summary>
            /// <param name="userId">The unique ID of the user.</param>            
            /// <param name="givenName">The user's given name. This is the first name in most cultures.</param>           
            public UpdateGivenName(Guid userId, string givenName)
            {
                UserId = userId;
                GivenName = givenName;
            }
        }

        /// <summary>
        /// Give name of a user was updated.
        /// </summary>
        public class GivenNameUpdated : Event
        {
            /// <summary>The unique ID of the user.</summary>
            public readonly Guid UserId;
            /// <summary>The user's given name. This is the first name in most cultures.</summary>
            public readonly string GivenName;
            /// <summary>
            /// Give name of a user was modified.
            /// </summary>
            /// <param name="userId">The unique ID of the user.</param>           
            /// <param name="givenName">The user's given name. This is the first name in most cultures.</param>            
            public GivenNameUpdated(Guid userId, string givenName)
            {
                UserId = userId;
                GivenName = givenName;
            }
        }

        /// <summary>
        /// Update a user's Surname.
        /// </summary>
        public class UpdateSurname : Command
        {
            /// <summary>The unique ID of the user.</summary>
            public readonly Guid UserId;
            /// <summary>The user's given name. This is the first name in most cultures.</summary>
            public readonly string Surname;

            /// <summary>
            /// Modify a user's Surname.
            /// </summary>
            /// <param name="userId">The unique ID of the user.</param>            
            /// <param name="givenName">The user's Surname. This is the last name in most cultures.</param>           
            public UpdateSurname(Guid userId, string surName)
            {
                UserId = userId;
                Surname = surName;
            }
        }

        /// <summary>
        /// Surname of a user was updated.
        /// </summary>
        public class SurnameUpdated : Event
        {
            /// <summary>The unique ID of the user.</summary>
            public readonly Guid UserId;
            /// <summary>The user's Surname. This is the last name in most cultures.</summary>
            public readonly string Surname;
            /// <summary>
            /// Surname of a user was modified.
            /// </summary>
            /// <param name="userId">The unique ID of the user.</param>           
            /// <param name="givenName">The user's surname. This is the first name in most cultures.</param>            
            public SurnameUpdated(Guid userId, string surName)
            {
                UserId = userId;
                Surname = surName;
            }
        }
        /// <summary>
        /// Update a user's full name.
        /// </summary>
        public class UpdateFullName : Command
        {
            /// <summary>The unique ID of the user.</summary>
            public readonly Guid UserId;
            /// <summary>The user's full name. This is the complete name in most cultures.</summary>
            public readonly string FullName;

            /// <summary>
            /// Modify a user's given name.
            /// </summary>
            /// <param name="userId">The unique ID of the user.</param>            
            /// <param name="fullName">The user's full name. This is the complete name in most cultures.</param>           
            public UpdateFullName(Guid userId, string fullName)
            {
                UserId = userId;
                FullName = fullName;
            }
        }

        /// <summary>
        /// full name of a user was updated.
        /// </summary>
        public class FullNameUpdated : Event
        {
            /// <summary>The unique ID of the user.</summary>
            public readonly Guid UserId;
            /// <summary>The user's full name. This is the complete name in most cultures.</summary>
            public readonly string FullName;
            /// <summary>
            /// Full name of a user was modified.
            /// </summary>
            /// <param name="userId">The unique ID of the user.</param>           
            /// <param name="givenName">The user's full name. This is the first name in most cultures.</param>            
            public FullNameUpdated(Guid userId, string fullName)
            {
                UserId = userId;
                FullName = fullName;
            }
        }
        /// <summary>
        /// Update a user's email.
        /// </summary>
        public class UpdateEmail : Command
        {
            /// <summary>The unique ID of the user.</summary>
            public readonly Guid UserId;
            /// <summary>The user's email address.</summary>
            public readonly string Email;

            /// <summary>
            /// Modify a user's email.
            /// </summary>
            /// <param name="userId">The unique ID of the user.</param>            
            /// <param name="email">The user's email.</param>           
            public UpdateEmail(Guid userId, string email)
            {
                UserId = userId;
                Email = email;
            }
        }

        /// <summary>
        /// email of a user was updated.
        /// </summary>
        public class EmailUpdated : Event
        {
            /// <summary>The unique ID of the user.</summary>
            public readonly Guid UserId;
            /// <summary>The user's email.</summary>
            public readonly string Email;
            /// <summary>
            /// email of a user was modified.
            /// </summary>
            /// <param name="userId">The unique ID of the user.</param>           
            /// <param name="email">The user's email address.</param>            
            public EmailUpdated(Guid userId, string email)
            {
                UserId = userId;
                Email = email;
            }
        }
        /// <summary>
        /// Update a user's UserSidFromAuthProvider.
        /// </summary>
        public class UpdateUserSidFromAuthProvider : Command
        {
            /// <summary>The unique ID of the user.</summary>
            public readonly Guid UserId;
            /// <summary>The unique ID from the auth provider (e.g. SID) of the authenticated user.</summary>
            public readonly string UserSidFromAuthProvider;

            /// <summary>
            /// Modify a user's given name.
            /// </summary>
            /// <param name="userId">The unique ID of the user.</param>            
            /// <param name="userSidFromAuthProvider">The user's UserSidFromAuthProvider.</param>           
            public UpdateUserSidFromAuthProvider(Guid userId, string userSidFromAuthProvider)
            {
                UserId = userId;
                UserSidFromAuthProvider = userSidFromAuthProvider;
            }
        }

        /// <summary>
        /// UserSidFromAuthProvider of a user was updated.
        /// </summary>
        public class UserSidFromAuthProviderUpdated : Event
        {
            /// <summary>The unique ID of the user.</summary>
            public readonly Guid UserId;
            /// <summary>The unique ID from the auth provider (e.g. SID) of the authenticated user.</summary>
            public readonly string UserSidFromAuthProvider;
            /// <summary>
            /// Give name of a user was modified.
            /// </summary>
            /// <param name="userId">The unique ID of the user.</param>           
            /// <param name="UserSidFromAuthProvider">The unique ID from the auth provider (e.g. SID) of the authenticated user.</param>            
            public UserSidFromAuthProviderUpdated(Guid userId, string userSidFromAuthProvider)
            {
                UserId = userId;
                UserSidFromAuthProvider = userSidFromAuthProvider;
            }
        }
        /// <summary>
        /// Update a user's AuthDomain.
        /// </summary>
        public class UpdateAuthDomain : Command
        {
            /// <summary>The unique ID of the user.</summary>
            public readonly Guid UserId;
            /// <summary>The user's AuthDomain. This is the AuthDomain.</summary>
            public readonly string AuthDomain;

            /// <summary>
            /// Modify a user's AuthDomain.
            /// </summary>
            /// <param name="userId">The unique ID of the user.</param>            
            /// <param name="authDomain">The user's AuthDomain.</param>           
            public UpdateAuthDomain(Guid userId, string authDomain)
            {
                UserId = userId;
                AuthDomain = authDomain;
            }
        }

        /// <summary>
        /// AuthDomain of a user was updated.
        /// </summary>
        public class AuthDomainUpdated : Event
        {
            /// <summary>The unique ID of the user.</summary>
            public readonly Guid UserId;
            /// <summary>The user's AuthDomain.</summary>
            public readonly string AuthDomain;
            /// <summary>
            /// Give name of a user was modified.
            /// </summary>
            /// <param name="userId">The unique ID of the user.</param>           
            /// <param name="AuthDomain">The user's AuthDomain.</param>            
            public AuthDomainUpdated(Guid userId, string authDomain)
            {
                UserId = userId;
                AuthDomain = authDomain;
            }
        }

        /// <summary>
        /// Update a user's UserName.
        /// </summary>
        public class UpdateUserName : Command
        {
            /// <summary>The unique ID of the user.</summary>
            public readonly Guid UserId;
            /// <summary>The user's UserName. </summary>
            public readonly string UserName;

            /// <summary>
            /// Modify a user's UserName.
            /// </summary>
            /// <param name="userId">The unique ID of the user.</param>            
            /// <param name="userName">The user's AuthDomain.</param>           
            public UpdateUserName(Guid userId, string userName)
            {
                UserId = userId;
                UserName = userName;
            }
        }

        /// <summary>
        /// UserName of a user was updated.
        /// </summary>
        public class UserNameUpdated : Event
        {
            /// <summary>The unique ID of the user.</summary>
            public readonly Guid UserId;
            /// <summary>The user's UserName.</summary>
            public readonly string UserName;
            /// <summary>
            /// Give name of a user was modified.
            /// </summary>
            /// <param name="userId">The unique ID of the user.</param>           
            /// <param name="userName">The user's UserName.</param>            
            public UserNameUpdated(Guid userId, string userName)
            {
                UserId = userId;
                UserName = userName;
            }
        }
        /// <summary>
        /// User data migrated.
        /// </summary>
        public class UserDataMigrated : Event
        {
            /// <summary>The unique ID of the role.</summary>
            public readonly Guid UserId;
            /// <summary>The stream data migrated to.</summary>
            public readonly string TargetStream;
            /// <summary>
            /// User data migrated.
            /// </summary>
            public UserDataMigrated(Guid userId,string targetStream)
            {
                UserId = userId;
                TargetStream = targetStream;
            }
        }
    }
}
