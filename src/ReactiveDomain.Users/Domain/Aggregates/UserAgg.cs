using System;
using System.Collections.Generic;
using System.Net.Mail;
using ReactiveDomain.Messaging;
using ReactiveDomain.Users.Messages;
using ReactiveDomain.Util;

namespace ReactiveDomain.Users.Domain.Aggregates
{
    /// <summary>
    /// Aggregate for a User.
    /// </summary>
    public class UserAgg : AggregateRoot
    {
        private List<Guid> AssignedPolicies { get; } = new List<Guid>();
        private List<Guid> AssignedRoles { get; } = new List<Guid>();
        private UserAgg()
        {
            RegisterEvents();
        }

        private void RegisterEvents()
        {
            Register<UserMsgs.UserCreated>(Apply);
            Register<UserMsgs.UserMigrated>(Apply);
            Register<UserPolicyMsgs.RoleAdded>(Apply);
            Register<UserPolicyMsgs.RoleRemoved>(Apply);
        }

        private void Apply(UserMsgs.UserCreated evt)
        {
            Id = evt.Id;
        }
        private void Apply(UserMsgs.UserMigrated evt)
        {
            Id = evt.Id;
        }

        private void Apply(UserPolicyMsgs.RoleAdded evt)
        {
            if (evt.UserId == Id)
                AssignedRoles.Add(evt.RoleId);
        }

        private void Apply(UserPolicyMsgs.RoleRemoved evt)
        {
            if (evt.UserId == Id)
                AssignedRoles.Remove(evt.RoleId);
        }

        /// <summary>
        /// Create a new user.
        /// </summary>
        public UserAgg(
            Guid id,
            string subjectId,
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
            Ensure.NotNullOrEmpty(subjectId, nameof(subjectId));

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
                         subjectId,
                         authProvider,
                         authDomain,
                         userName,
                         fullName,
                         givenName,
                         surname,
                         email));
        }
        public void MapSubject(SubjectAgg subject)
        {   
            Raise(new UserPolicyMsgs.MapSubject(Id, subject.SubClaim, subject.AuthProvider, subject.AuthDomain));
        }
        public void RemoveSubject(SubjectAgg subject)
        {
            Raise(new UserPolicyMsgs.RemoveSubject(Id, subject.SubClaim, subject.AuthProvider, subject.AuthDomain));
        }
        /// <summary>
        /// Assign a policy to the user.
        /// </summary>
        public void AddPolicy(SecurityPolicyAgg policy)
        {
            Ensure.NotNull(policy, nameof(policy));
            if (AssignedPolicies.Contains(policy.Id)) return;
            Raise(new UserPolicyMsgs.PolicyAdded(Id, policy.Id));
        }

        /// <summary>
        /// remove a policy from a user.
        /// </summary>
        public void RemovePolicy(SecurityPolicyAgg policy)
        {
            Ensure.NotNull(policy, nameof(policy));
            if (AssignedPolicies.Contains(policy.Id))
            {
                foreach (var role in policy.Roles)
                {
                    if (AssignedRoles.Contains(role))
                    {
                        Raise(new UserPolicyMsgs.RemoveRole(Id, role));
                    }
                }
                Raise(new UserPolicyMsgs.PolicyRemoved(Id, policy.Id));
            }
        }

        /// <summary>
        /// Add a role to the user.
        /// </summary>
        public void AddRole(Guid roleId)
        {
            Ensure.NotEmptyGuid(roleId, nameof(roleId));
            if (AssignedRoles.Contains(roleId)) return;
            Raise(new UserPolicyMsgs.RoleAdded(Id, roleId));
        }

        /// <summary>
        /// Unassign a role from a user.
        /// </summary>
        public void RemoveRole(Guid roleId)
        {
            Ensure.NotEmptyGuid(roleId, nameof(roleId));
            if (AssignedRoles.Contains(roleId))
            {
                Raise(new UserPolicyMsgs.RoleRemoved(Id, roleId));
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
