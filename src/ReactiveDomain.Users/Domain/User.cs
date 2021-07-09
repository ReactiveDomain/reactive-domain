using System;
using System.Collections.Generic;
using System.Net.Mail;
using ReactiveDomain.Messaging;
using ReactiveDomain.Users.Messages;
using ReactiveDomain.Util;

namespace ReactiveDomain.Users.Domain
{
    /// <summary>
    /// Aggregate for a User.
    /// </summary>
    public class User : AggregateRoot
    {
        private string _fullName;
        private string _givenName;
        private string _surname;
        private string _email;
        private readonly HashSet<string> _clientScopes = new HashSet<string>();
        private User()
        {
            RegisterEvents();
        }

        private void RegisterEvents()
        {
            Register<UserMsgs.UserCreated>(Apply);
            Register<UserMsgs.UserDetailsUpdated>(Apply);
            Register<UserMsgs.AddClientScope>(Apply);
            Register<UserMsgs.RemoveClientScope>(Apply);
        }

        private void Apply(UserMsgs.UserCreated evt)
        {
            Id = evt.UserId;         
        }
        private void Apply(UserMsgs.UserDetailsUpdated evt)
        {
            _fullName = evt.FullName;
            _givenName = evt.GivenName;
            _surname = evt.Surname;
            _email = evt.Email;
        }

        private void Apply(UserMsgs.AddClientScope evt)
        {
            _clientScopes.Add(evt.ClientScope);
        }
        private void Apply(UserMsgs.RemoveClientScope evt)
        {
            _clientScopes.Remove(evt.ClientScope);
        }
        /// <summary>
        /// Create a new user.
        /// </summary>
        public User(
            Guid id,
            string fullName,
            string givenName,
            string surname,
            string email,
            ICorrelatedMessage source)
            : this()
        {
            Ensure.NotEmptyGuid(id, nameof(id));
            // ReSharper disable once ObjectCreationAsStatement
            if (!string.IsNullOrEmpty(email))
                new MailAddress(email);  // performs validation on the provided address.

            Ensure.NotNull(source, nameof(source));
            Ensure.NotEmptyGuid(source.CorrelationId, nameof(source.CorrelationId));
            if (source.CausationId == Guid.Empty)
                Ensure.NotEmptyGuid(source.MsgId, nameof(source.MsgId));

            ((ICorrelatedEventSource)this).Source = source;
            Raise(new UserMsgs.UserCreated(id));
            Raise(new UserMsgs.UserDetailsUpdated(
                                     id,
                                     givenName,
                                     surname,
                                     fullName,
                                     email));

        }
        public void MapToAuthDomain(
            string subjectId,
            string authProvider,
            string authDomain,
            string userName)
        {
            Raise(new UserMsgs.AuthDomainMapped(Id, subjectId, authProvider, authDomain, userName));
        }

        /// <summary>
        /// Assign a Client Scope to the user.
        /// </summary>
        public void AddClientScope(string scope)
        {
            if (string.IsNullOrWhiteSpace(scope)) { throw new ArgumentOutOfRangeException(nameof(scope), "Cannot add null, empty, or whitespace scope to User");}
            scope = scope.ToUpper();
            if (_clientScopes.Contains(scope)) { return;}
            Raise(new UserMsgs.ClientScopeAdded(Id, scope));
        }
        /// <summary>
        /// Remove a Client Scope from the user.
        /// </summary>
        public void RemoveClientScope(string scope)
        {
            if (string.IsNullOrWhiteSpace(scope)) { throw new ArgumentOutOfRangeException(nameof(scope), "Cannot add null, empty, or whitespace scope to User"); }
            scope = scope.ToUpper();
            if (_clientScopes.Contains(scope)) { return; }
            Raise(new UserMsgs.ClientScopeRemoved(Id, scope));
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
        /// Update Name Details
        /// </summary>
        public void UpdateNameDetails(
            string givenName = null,
            string surName = null,
            string fullName = null,
            string email = null)
        {
            if (IsChange(givenName, _givenName) ||
                IsChange(surName, _surname) ||
                IsChange(fullName, _fullName) ||
                IsChange(email, _email))
            {
                Raise(new UserMsgs.UserDetailsUpdated(
                            Id,
                            givenName ?? _givenName,
                            surName ?? _surname,
                            fullName ?? _fullName,
                            email ?? _email));
            }
        }
        private bool IsChange(string input, string existing) {
            if (input == null) { return false; }
            return string.Equals(input, existing, StringComparison.Ordinal);
        }

    }
}
