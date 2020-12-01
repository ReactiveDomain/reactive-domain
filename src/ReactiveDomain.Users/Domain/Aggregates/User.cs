using System;
using System.Collections.Generic;
using System.Net.Mail;
using Elbe.Messages;
using ReactiveDomain;
using ReactiveDomain.Messaging;
using ReactiveDomain.Util;

namespace Elbe.Domain
{
    /// <summary>
    /// Aggregate for a User.
    /// </summary>
    public class User : AggregateRoot
    {
        private List<Guid> AssignedRoles { get; } = new List<Guid>();
        private User()
        {
            RegisterEvents();
        }

        private void RegisterEvents()
        {
            Register<UserMsgs.UserCreated>(Apply);
            Register<UserMsgs.UserMigrated>(Apply);
            Register<UserMsgs.RoleAssigned>(Apply);
            Register<UserMsgs.RoleUnassigned>(Apply);
        }

        private void Apply(UserMsgs.UserCreated evt)
        {
            Id = evt.Id;
        }
        private void Apply(UserMsgs.UserMigrated evt)
        {
            Id = evt.Id;
        }

        private void Apply(UserMsgs.RoleAssigned evt)
        {
            if (evt.UserId == Id)
                AssignedRoles.Add(evt.RoleId);
        }

        private void Apply(UserMsgs.RoleUnassigned evt)
        {
            if (evt.UserId == Id)
                AssignedRoles.Remove(evt.RoleId);
        }

        /// <summary>
        /// Create a new user.
        /// </summary>
        public User(
            Guid id,
            string userSidFromAuthProvider,
            string authProvider,
            string authDomain,
            string userName,
            string fullName,
            string givenName,
            string surname,
            string email,
            ICorrelatedMessage source)
            : this()
        {
            Ensure.NotEmptyGuid(id, nameof(id));
            Ensure.NotNullOrEmpty(authProvider, nameof(authProvider));
            Ensure.NotNullOrEmpty(authDomain, nameof(authDomain));
            Ensure.NotNullOrEmpty(userName, nameof(userName));

            // ReSharper disable once ObjectCreationAsStatement
            if (!string.IsNullOrEmpty(email))
                new MailAddress(email);  // performs validation on the provided address.

            Ensure.NotNull(source, nameof(source));
            Ensure.NotEmptyGuid(source.CorrelationId, nameof(source.CorrelationId));
            if (source.CausationId == Guid.Empty)
                Ensure.NotEmptyGuid(source.MsgId, nameof(source.MsgId));

            ((ICorrelatedEventSource)this).Source = source;
            Raise(new UserMsgs.UserCreated(
                         id,
                         userSidFromAuthProvider,
                         authProvider,
                         authDomain,
                         userName,
                         fullName,
                         givenName,
                         surname,
                         email));
        }

        /// <summary>
        /// Log the fact that a user has been successfully authenticated.
        /// </summary>
        public void Authenticated(string hostIPAddress)
        {
            Raise(new UserMsgs.Authenticated(
                        Id,
                        DateTime.UtcNow,
                        hostIPAddress));
        }
        /// <summary>
        /// Log the fact that a user has not been successfully authenticated.
        /// </summary>
        public void NotAuthenticated(string hostIPAddress)
        {
            Raise(new UserMsgs.AuthenticationFailed(
                        Id,
                        DateTime.UtcNow,
                        hostIPAddress));
        }
        /// <summary>
        /// Log the fact that a user has not been successfully authenticated because account is locked.
        /// </summary>
        public void NotAuthenticatedAccountLocked(string hostIPAddress)
        {
            Raise(new UserMsgs.AuthenticationFailedAccountLocked(
                        Id,
                        DateTime.UtcNow,
                        hostIPAddress));
        }
        /// <summary>
        /// Log the fact that a user has not been successfully authenticated because account is disabled.
        /// </summary>
        public void NotAuthenticatedAccountDisabled(string hostIPAddress)
        {
            Raise(new UserMsgs.AuthenticationFailedAccountDisabled(
                        Id,
                        DateTime.UtcNow,
                        hostIPAddress));
        }
        /// <summary>
        /// Log the fact that a user has not been successfully authenticated because invalid credentials were supplied.
        /// </summary>
        public void NotAuthenticatedInvalidCredentials(string hostIPAddress)
        {
            Raise(new UserMsgs.AuthenticationFailedInvalidCredentials(
                        Id,
                        DateTime.UtcNow,
                        hostIPAddress));
        }

        /// <summary>
        /// Assign a role to the user.
        /// </summary>
        public void AssignRole(Guid roleId)
        {
            Ensure.NotEmptyGuid(roleId, nameof(roleId));
            if (AssignedRoles.Contains(roleId)) return;
            Raise(new UserMsgs.RoleAssigned(Id, roleId));
        }

        /// <summary>
        /// Unassign a role from a user.
        /// </summary>
        public void UnassignRole(Guid roleId)
        {
            Ensure.NotEmptyGuid(roleId, nameof(roleId));
            if (AssignedRoles.Contains(roleId))
            {
                Raise(new UserMsgs.RoleUnassigned(Id, roleId));
            }
        }

        /// <summary>
        /// Deactivate the user.
        /// </summary>
        public void Deactivate()
        {
            Raise(new UserMsgs.Deactivated(Id));
        }

        /// <summary>
        /// Reactivate the user.
        /// </summary>
        public void Reactivate()
        {
            Raise(new UserMsgs.Activated(Id));
        }
        /// <summary>
        /// Log the fact that a user's given name has been updated.
        /// </summary>
        public void UpdateGivenName(string givenName)
        {
            Ensure.NotNullOrEmpty(givenName, nameof(givenName));
            Raise(new UserMsgs.GivenNameUpdated(
                        Id,
                        givenName));
        }
        /// <summary>
        /// Log the fact that a user's surname has been updated.
        /// </summary>
        public void UpdateSurname(string surName)
        {
            Ensure.NotNullOrEmpty(surName, nameof(surName));
            Raise(new UserMsgs.SurnameUpdated(
                        Id,
                        surName));
        }
        /// <summary>
        /// Log the fact that a user's fullname has been updated.
        /// </summary>
        public void UpdateFullName(string fullName)
        {
            Ensure.NotNullOrEmpty(fullName, nameof(fullName));
            Raise(new UserMsgs.FullNameUpdated(
                        Id,
                        fullName));
        }
        /// <summary>
        /// Log the fact that a user's email has been updated.
        /// </summary>
        public void UpdateEmail(string email)
        {
            Ensure.NotNullOrEmpty(email, nameof(email));
            _ = new MailAddress(email);  // performs validation on the provided email.
            Raise(new UserMsgs.EmailUpdated(
                        Id,
                        email));
        }
        /// <summary>
        /// Log the fact that a user's UserSidFromAuthProvider has been updated.
        /// </summary>
        public void UpdateUserSidFromAuthProvider(string userSidFromAuthProvider)
        {
            Ensure.NotNullOrEmpty(userSidFromAuthProvider, nameof(userSidFromAuthProvider));
            Raise(new UserMsgs.UserSidFromAuthProviderUpdated(
                        Id,
                        userSidFromAuthProvider));
        }
        /// <summary>
        /// Log the fact that a user's AuthDomain has been updated.
        /// </summary>
        public void UpdateAuthDomain(string authDomain)
        {
            Ensure.NotNullOrEmpty(authDomain, nameof(authDomain));
            Raise(new UserMsgs.AuthDomainUpdated(
                        Id,
                        authDomain));
        }
        /// <summary>
        /// Log the fact that a user's UserName has been updated.
        /// </summary>
        public void UpdateUserName(string userName)
        {
            Ensure.NotNullOrEmpty(userName, nameof(userName));
            Raise(new UserMsgs.UserNameUpdated(
                        Id,
                        userName));
        }
    }
}
