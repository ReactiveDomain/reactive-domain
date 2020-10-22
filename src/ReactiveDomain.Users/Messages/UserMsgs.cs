using System;
using ReactiveDomain.Messaging;

namespace ReactiveDomain.Users.Messages
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
            public readonly Guid UserId;
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
            /// <param name="userId">The unique ID of the new user.</param>         
            /// <param name="givenName">The user's given name. This is the first name in most cultures.</param>
            /// <param name="surname">The user's surname or family name. This is the last name in most cultures.</param>
            /// <param name="email">The user's email address.</param>
            /// <param name="fullName">user full name.</param>
            public CreateUser(
                Guid userId,
                string fullName,
                string givenName,
                string surname,
                string email
                )
            {
                UserId = userId;
                FullName = fullName;
                GivenName = givenName;
                Surname = surname;
                Email = email;

            }
        }
        public class UserEvent : Event
        {
            public Guid UserId;
        }

        /// <summary>
        /// A new user was created.
        /// </summary>
        public class UserCreated : UserEvent
        {

            /// <summary>
            /// A new user was created.
            /// </summary>         
            public UserCreated(Guid userId)
            {
                UserId = userId;
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
        public class Deactivated : UserEvent
        {

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
        public class Activated : UserEvent
        {
            /// <summary>
            /// User is activated.
            /// </summary>
            public Activated(Guid userId)
            {
                UserId = userId;
            }
        }

        /// <summary>
        /// Update User Details
        /// </summary>
        public class UpdateUserDetails : Command
        {

            public readonly Guid UserId;
            public readonly string GivenName;
            public readonly string Surname;
            public readonly string FullName;
            public readonly string Email;

            /// <summary>
            /// Modify a user's name details.
            /// </summary>

            public UpdateUserDetails(
                Guid userId,
                string givenName = null,
                string surname = null,
                string fullName = null,
                string email = null)
            {
                UserId = userId;
                GivenName = givenName;
                Surname = surname;
                FullName = fullName;
                Email = email;
            }
        }

        /// <summary>
        /// User Details Updated
        /// </summary>
        public class UserDetailsUpdated : UserEvent
        {
            public readonly string GivenName;
            public readonly string Surname;
            public readonly string FullName;
            public readonly string Email;

            /// <summary>
            /// Modify a user's name details.
            /// </summary>
            public UserDetailsUpdated(
                Guid userId,
                string givenName,
                string surname,
                string fullName,
                string email)
            {
                UserId = userId;
                GivenName = givenName;
                Surname = surname;
                FullName = fullName;
                Email = email;
            }
        }

        /// <summary>
        /// Update a user's AuthDomain information.
        /// </summary>
        public class MapToAuthDomain : Command
        {
            /// <summary>The unique ID of the user.</summary>
            public readonly Guid UserId;
            /// <summary>The unique ID from the auth provider (e.g. Sub claim) of the authenticated user.</summary>
            public readonly string SubjectId;
            /// <summary>The identity provider.</summary>
            public readonly string AuthProvider;
            /// <summary>The user's domain.</summary>
            public readonly string AuthDomain;
            /// <summary>The username, which should be unique within the <see cref="AuthDomain"/>.</summary>
            public readonly string UserName;

            /// <summary>
            /// Modify a user's AuthDomain information.
            /// </summary>
            ///<param name="subjectId">The unique ID from the auth provider (e.g. Sub Claim) of the authenticated user.</param>
            /// <param name="authProvider">The identity provider.</param>
            /// <param name="authDomain">The user's domain.</param>
            /// <param name="userName">The username, which should be unique within the <see cref="AuthDomain"/>.</param>     
            public MapToAuthDomain(
                Guid userId,
                string subjectId,
                string authProvider,
                string authDomain,
                string userName)
            {
                UserId = userId;
                SubjectId = subjectId;
                AuthProvider = authProvider;
                AuthDomain = authDomain;
                UserName = userName;
            }
        }

        /// <summary>
        /// AuthDomain of a user was updated.
        /// </summary>
        public class AuthDomainMapped : UserEvent
        {
            /// <summary>The unique ID from the auth provider (e.g. Sub claim) of the authenticated user.</summary>
            public readonly string SubjectId;
            /// <summary>The identity provider.</summary>
            public readonly string AuthProvider;
            /// <summary>The user's domain.</summary>
            public readonly string AuthDomain;
            /// <summary>The username, which should be unique within the <see cref="AuthDomain"/>.</summary>
            public readonly string UserName;

            /// <summary>
            /// User's AuthDomain information updated.
            /// </summary>
            ///<param name="subjectId">The unique ID from the auth provider (e.g. Sub Claim) of the authenticated user.</param>
            /// <param name="authProvider">The identity provider.</param>
            /// <param name="authDomain">The user's domain.</param>
            /// <param name="userName">The username, which should be unique within the <see cref="AuthDomain"/>.</param>     
            public AuthDomainMapped(
                Guid userId,
                string subjectId,
                string authProvider,
                string authDomain,
                string userName)
            {
                UserId = userId;
                SubjectId = subjectId;
                AuthProvider = authProvider;
                AuthDomain = authDomain;
                UserName = userName;
            }
        }

        /// <summary>
        /// Add Client Scope to a user.
        /// </summary>
        public class AddClientScope : Command
        {
            /// <summary> The ID of the user.</summary>
            public readonly Guid UserId;
            public readonly string ClientScope;


            /// <summary>
            /// Add Client Scope.
            /// </summary>
            public AddClientScope(Guid userId, string clientScope)
            {
                UserId = userId;
                ClientScope = clientScope;
            }
        }

        /// <summary>
        /// Client Scope Added to a user.
        /// </summary>
        public class ClientScopeAdded : UserEvent
        {
            public readonly string ClientScope;

            /// <summary>
            /// Client Scope Added
            /// </summary>
            public ClientScopeAdded(Guid userId, string clientScope)
            {
                UserId = userId;
                ClientScope = clientScope;
            }
        }

        /// <summary>
        /// Remove Client Scope from a user.
        /// </summary>
        public class RemoveClientScope : Command
        {
            /// <summary> The ID of the user.</summary>
            public readonly Guid UserId;
            public readonly string ClientScope;


            /// <summary>
            /// Remove Client Scope.
            /// </summary>
            public RemoveClientScope(Guid userId, string clientScope)
            {
                UserId = userId;
                ClientScope = clientScope;
            }
        }

        /// <summary>
        /// Client Scope Removed from a user.
        /// </summary>
        public class ClientScopeRemoved : UserEvent
        {
            public readonly string ClientScope;
            /// <summary>
            /// Client Scope Removed
            /// </summary>
            public ClientScopeRemoved(Guid userId, string clientScope)
            {
                UserId = userId;
                ClientScope = clientScope;
            }
        }

    }
}
