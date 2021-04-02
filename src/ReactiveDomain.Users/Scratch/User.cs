using System;
using System.Collections.Generic;
using System.Security.Claims;

using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;

namespace ReactiveDomain.Users.Scratch
{
    /// <summary>
    /// This represents an "internal" user within the system.  It aggregates one or more external subjects
    /// </summary>
    public class User : AggregateRoot
    {
        private readonly Dictionary<string, HashSet<string>> _authorities = new Dictionary<string, HashSet<string>>();
        private bool _active = false;

        /// <summary>
        /// When a user is created from an external authority, such as FaceBook, Windows Authentication, etc.
        /// </summary>
        /// <param name="id">The id of the user, generated before creation</param>
        /// <param name="principal">The principal as constructed through validation from the external authority.</param>
        /// <param name="authority">The name of the authority (e.g. facebook, linkedin, etc.)</param>
        /// <param name="domain">If required, </param>
        /// <param name="msg"></param>
        public User(Guid id, ClaimsPrincipal principal, string authority, string domain = default, ICorrelatedMessage msg = default)
            : base(msg)
        {
            
        }

        /// <summary>
        /// If a user registers through a registration or sign-up form.
        /// </summary>
        /// <param name="id">The id of the user, generated before creation</param>
        /// <param name="username">The requested username</param>
        /// <param name="firstName">The user's first name</param>
        /// <param name="lastName">The user's last name</param>
        /// <param name="email">The user's provided email</param>
        /// <param name="autoGeneratePassword">When true, create a generic password that is then emailed to the user for their first login.</param>
        /// <param name="hasher">The tool to properly hash the provided password, should the flag be set to automatically generate a password.</param>
        /// <param name="msg">The originating message that caused this action.  This is usually a command.</param>
        public User(Guid id, string username, string firstName, string lastName, string email, bool autoGeneratePassword, object hasher, ICorrelatedMessage msg = default) 
            : base(msg)
        {
            
        }

        /// <summary>
        /// Never use this.  This is intended for use as part of the hydration process for the aggregate.
        /// </summary>
        public User():base(null) { }

        #region Subject
        // I want to discuss this a little more in-depth.  "Subjects" felt initially like an aspect itself of
        // a user, but the more I think about it (and we need to start refactoring to it to truly identify this)
        // I'm starting to think that maybe there will be enough "bones" that having a ChildEntity would be a better definiton.
        // I'll keep this as-is for now, but it's something to be discussed and explored.
        
        public void RegisterAlias(ClaimsPrincipal principal, string authority, string domain = default)
        {
            Raise(new UserMsgs.AliasRegistered(Id, authority, domain));
        }

        /// <summary>
        /// Allows the ability to remove an alias (think Facebook, LinkedIn, etc. OAuth/OpenIDConnect) relations to
        /// the system's internal users.
        /// </summary>
        /// <param name="authority"></param>
        /// <param name="domain"></param>
        public void RemoveAlias(string authority, string domain = default)
        {
            Raise(new UserMsgs.AliasRemoved(Id, authority, domain));
        }
        
        #endregion

        /// <summary>
        /// Updates the users "username".  Before this is called, a read model validation should occur to ensure
        /// that the requested new username is not "currently" utilized by another user within the system.
        /// </summary>
        /// <param name="userName"></param>
        public void UpdateUsername(string userName)
        {
            Raise(new UserMsgs.UsernameUpdated(Id, userName));
        }

        /// <summary>
        /// Updates the users given name (first name)
        /// </summary>
        /// <param name="givenName"></param>
        public void UpdateGivenName(string givenName)
        {
            Raise(new UserMsgs.GivenNameUpdated(Id, givenName));
        }

        /// <summary>
        /// Updates the user's surname (last name)
        /// </summary>
        /// <param name="surName"></param>
        public void UpdateSurname(string surName)
        {
            Raise(new UserMsgs.SurnameUpdated(Id, surName));
        }

        /// <summary>
        /// Updates the user's full name.
        /// <remarks>Need to discuss with CC, do we want to have unique email addresses, or do we care?</remarks>
        /// </summary>
        /// <param name="fullName"></param>
        public void UpdateFullName(string fullName)
        {
            Raise(new UserMsgs.FullNameUpdated(Id, fullName));
        }

        /// <summary>
        /// Updates the user's email account.
        /// <remarks>This may trigger an email, but the email may come from the <see cref="UserMsgs.EmailUpdated" /> event.</remarks>
        /// </summary>
        /// <param name="email"></param>
        public void UpdateEmail(string email)
        {
            Raise(new UserMsgs.EmailUpdated(Id, email));
        }

        // These two most likely should be owned by the application itself, as users themselves only know about
        // themselves and not the applications they work with.
        public void AddRoles(IEnumerable<string> roles) { }
        public void RemoveRoles(IEnumerable<string> roles) { }
        
        /// <summary>
        /// Deactivates a user, which prevents them from logging in.
        /// </summary>
        public void Deactivate() { }
        
        /// <summary>
        /// Re-activates a deactivated (or dormant) user.
        /// </summary>
        public void Reactivate() { }

        /// <summary>
        /// When a user has lost their password, and is found through their email address or other means, this
        /// starts the reset process.
        /// </summary>
        /// <param name="emailService"></param>
        /// <param name="laterService">Later Service to schedule a timeout.</param>
        public void RequestPasswordReset(object emailService, LaterService laterService) { }
        
        /// <summary>
        /// When the user receives the email and clicks on the embedded link to complete the password reset, this is
        /// the method that completes the request.
        /// </summary>
        /// <param name="requestKey"></param>
        /// <param name="newPassword"></param>
        /// <param name="hasher"></param>
        public void CompletePasswordReset(string requestKey, string newPassword, object hasher) { }
        
        /// <summary>
        /// Changes the user password after validating that the old password is valid, and the new password is confirmed.
        /// </summary>
        /// <param name="oldPassword"></param>
        /// <param name="newPassword"></param>
        /// <param name="newPasswordConfirmation"></param>
        /// <param name="hasher"></param>
        public void ChangePassword(string oldPassword, string newPassword, string newPasswordConfirmation, object hasher) { }
        
        /// <summary>
        /// Allows an admin to set a password for a user.  This should most likely *NOT* be available, but there are
        /// circumstances where it is warranted.  Use with extreme caution.
        /// </summary>
        /// <param name="password">The password that should be set for the new user.</param>
        /// <param name="hasher">The tool to properly hash the password into something that cannot be easily decoded.</param>
        public void SetPassword(string password, object hasher) { }
    }
}