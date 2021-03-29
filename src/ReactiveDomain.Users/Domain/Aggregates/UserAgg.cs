using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Users.Domain.Services;
using ReactiveDomain.Users.Messages;
using ReactiveDomain.Users.ReadModels;
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
        private string _currentPassword;
        private Queue<PasswordHistory> _historicalPasswords = new Queue<PasswordHistory>();
        private int _maxHistoricalPasswords;
        private int _maxPasswordAge;
        private int _requiredLength;
        private int _requiredUniqueCharacters;
        private bool _requireNonAlphanumeric;
        private bool _requireLowercase;
        private bool _requireUppercase;
        private bool _requireDigit;
        private UserAgg()
        {
            RegisterEvents();
        }

        private void RegisterEvents()
        {
            Register<UserMsgs.UserCreated>(Apply);
            Register<UserMsgs.UserMigrated>(Apply);
            Register<UserMsgs.PasswordSettingsChanged>(Apply);
            Register<UserMsgs.PasswordSet>(Apply);
            Register<UserMsgs.PasswordChanged>(Apply);
            Register<UserMsgs.PasswordCleared>(Apply);
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
        private void Apply(UserMsgs.PasswordSettingsChanged evt)
        {
            //Note: This is a work-in-progress.  Determining what is needed for enforcement.
            _requiredLength = evt.RequiredLength;
            _requiredUniqueCharacters = evt.RequiredUniqueCharacters;
            _requireNonAlphanumeric = evt.RequireNonAlphanumeric;
            _requireLowercase = evt.RequireLowercase;
            _requireUppercase = evt.RequireUppercase;
            _requireDigit = evt.RequireDigit;
            _maxHistoricalPasswords = evt.MaxHistoricalPasswords;
            _maxPasswordAge = evt.MaxPasswordAge;
        }
        private void Apply(UserMsgs.PasswordSet evt)
        {
            _currentPassword = evt.PasswordHash;
            _historicalPasswords.Enqueue(new PasswordHistory(DateTime.UtcNow, evt.PasswordHash));
            while(_historicalPasswords.Count > _maxHistoricalPasswords)
            {
                _historicalPasswords.Dequeue();
            }
        }
        private void Apply(UserMsgs.PasswordChanged evt)
        {
            _currentPassword = evt.PasswordHash;
            _historicalPasswords.Enqueue(new PasswordHistory(DateTime.UtcNow, evt.PasswordHash));
            while (_historicalPasswords.Count > _maxHistoricalPasswords)
            {
                _historicalPasswords.Dequeue();
            }
        }
        private void Apply(UserMsgs.PasswordCleared evt)
        {
            _currentPassword = default;
            _historicalPasswords.Clear();
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

        /// <summary>
        /// Disables local password authentication.
        /// </summary>
        public void DisablePasswordAuthentication()
        {
            if (!string.IsNullOrWhiteSpace(_currentPassword))
            {
                Raise(new UserMsgs.PasswordCleared(Id));
            }
        }

        /// <summary>
        /// Sets a password for this user.
        /// </summary>
        /// <param name="password"></param>
        /// <param name="options"></param>
        /// <param name="hasher"></param>
        public void SetPassword(string password, PasswordOptions options, IPasswordHasher hasher, ITimeSource timeSource)
        {
            Ensure.NotNullOrEmpty(password, nameof(password));

            // hash password
            var hashed = hasher.HashPassword(password);

            // ensure new password hash is not historically used.
            if (_currentPassword?.Equals(hashed) ?? false) { return; } // no need to trigger an event when there is a no-op here.
            if (_historicalPasswords.Any(h => h.PasswordHash == hashed)) { throw new Exception("Cannot set a password that has historically been used."); }

            // validate password for complexity.
            ValidatePasswordComplexity(password, options);

            AttemptToRaisePasswordSettingsChanged(options);

            var expiresIn = options.MaxPasswordAge > 0
                ? new TimeSpan(options.MaxPasswordAge, 0, 0, 0)
                : default;

            // emit event PasswordSet
            Raise(new UserMsgs.PasswordSet(Id, hashed, expiresIn));
        }

        /// <summary>
        /// Changes the user's password.
        /// </summary>
        public void ChangePassword(string oldPassword, string newPassword, string confirmedNewPassword, PasswordOptions options, IPasswordHasher hasher, ITimeSource timeSource)
        {
            Ensure.NotNullOrEmpty(oldPassword, nameof(oldPassword)); // argument null exception
            Ensure.NotNullOrEmpty(newPassword, nameof(newPassword)); // argument null exception
            Ensure.NotNullOrEmpty(confirmedNewPassword, nameof(confirmedNewPassword)); // argument null exception

            // ensure that old password and new password are not equal
            Ensure.False(() => oldPassword.Equals(newPassword), "New password cannot be the same as the current password."); // argument exception

            // ensure that new password and new password confirmation are equal.
            Ensure.True(() => newPassword.Equals(confirmedNewPassword), "New password and confirmed new password do not match."); // argument exception

            var oldPasswordHash = hasher.HashPassword(oldPassword);
            if (!_currentPassword.Equals(oldPasswordHash)) { throw new Exception("Old password is invalid."); }

            // hash new password
            var hashed = hasher.HashPassword(newPassword);

            // if current and new passwords are equivalent, this becomes a no-op.
            if (_currentPassword.Equals(hashed)) return;

            // ensure new password hash is not historically used.
            if (_historicalPasswords.Any(h => h.PasswordHash == hashed)) { throw new Exception("Cannot set a password that has historically been used."); }

            // validate new password for complexity
            ValidatePasswordComplexity(newPassword, options);

            AttemptToRaisePasswordSettingsChanged(options);

            var expiresIn = options.MaxPasswordAge > 0
                ? new TimeSpan(options.MaxPasswordAge, 0, 0, 0)
                : default;

            // emit event PasswordReset
            Raise(new UserMsgs.PasswordChanged(Id, hashed, expiresIn));
        }

        private void AttemptToRaisePasswordSettingsChanged(PasswordOptions options)
        {
            if (
                _requiredLength != options.RequiredLength ||
                _requiredUniqueCharacters != options.RequiredUniqueCharacters ||
                _requireNonAlphanumeric != options.RequireNonAlphanumeric ||
                _requireLowercase != options.RequireLowercase ||
                _requireUppercase != options.RequireUppercase ||
                _requireDigit != options.RequireDigit ||
                _maxHistoricalPasswords != options.MaxHistoricalPasswords ||
                _maxPasswordAge != options.MaxPasswordAge
            )
            {
                Raise(new UserMsgs.PasswordSettingsChanged(
                    options.RequiredLength, 
                    options.RequiredUniqueCharacters, 
                    options.RequireNonAlphanumeric, 
                    options.RequireLowercase, 
                    options.RequireUppercase, 
                    options.RequireDigit,
                    options.MaxHistoricalPasswords,
                    options.MaxPasswordAge));
            }
        }

        /// <summary>
        /// Extraction of the password complexity validation rules.
        /// </summary>
        /// <param name="password"></param>
        /// <param name="identityOptions"></param>
        private void ValidatePasswordComplexity(string password, PasswordOptions options)
        {
            var errors = new List<IdentityError>();
            if (string.IsNullOrEmpty(password) || password.Length < options.RequiredLength) { errors.Add(Describer.PasswordTooShort(options.RequiredLength)); }
            if (options.RequireNonAlphanumeric && password.All(IsLetterOrDigit)) { errors.Add(Describer.PasswordRequiresNonAlphanumeric()); }
            if (options.RequireDigit && !password.Any(IsDigit)) { errors.Add(Describer.PasswordRequiresDigit()); }
            if (options.RequireLowercase && !password.Any(IsLower)) { errors.Add(Describer.PasswordRequiresLower()); }
            if (options.RequireUppercase && !password.Any(IsUpper)) { errors.Add(Describer.PasswordRequiresUpper()); }
            if (options.RequiredUniqueCharacters > 1 && password.Distinct().Count() < options.RequiredUniqueCharacters) { errors.Add(Describer.PasswordRequiresUniqueCharacters(options.RequiredUniqueCharacters)); }

            if (errors.Count > 0) { throw new IdentityErrorException(errors); }
        }

        /// <summary>
        /// Returns a flag indicating whether the supplied character is a digit.
        /// </summary>
        /// <param name="c">The character to check if it is a digit.</param>
        /// <returns>True if the character is a digit, otherwise false.</returns>
        public virtual bool IsDigit(char c)
        {
            return c >= '0' && c <= '9';
        }

        /// <summary>
        /// Returns a flag indicating whether the supplied character is a lower case ASCII letter.
        /// </summary>
        /// <param name="c">The character to check if it is a lower case ASCII letter.</param>
        /// <returns>True if the character is a lower case ASCII letter, otherwise false.</returns>
        public virtual bool IsLower(char c)
        {
            return c >= 'a' && c <= 'z';
        }

        /// <summary>
        /// Returns a flag indicating whether the supplied character is an upper case ASCII letter.
        /// </summary>
        /// <param name="c">The character to check if it is an upper case ASCII letter.</param>
        /// <returns>True if the character is an upper case ASCII letter, otherwise false.</returns>
        public virtual bool IsUpper(char c)
        {
            return c >= 'A' && c <= 'Z';
        }

        /// <summary>
        /// Returns a flag indicating whether the supplied character is an ASCII letter or digit.
        /// </summary>
        /// <param name="c">The character to check if it is an ASCII letter or digit.</param>
        /// <returns>True if the character is an ASCII letter or digit, otherwise false.</returns>
        public virtual bool IsLetterOrDigit(char c)
        {
            return IsUpper(c) || IsLower(c) || IsDigit(c);
        }

        class PasswordHistory
        {
            public readonly DateTime DateChanged;
            public readonly string PasswordHash;

            public PasswordHistory(DateTime dateChanged, string passwordHash)
            {
                DateChanged = dateChanged;
                PasswordHash = passwordHash;
            }
        }
    }
}
