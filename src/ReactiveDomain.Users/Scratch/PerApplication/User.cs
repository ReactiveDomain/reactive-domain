using System;
using System.Collections.Generic;

namespace ReactiveDomain.Users.Scratch.PerApplication
{
    /// <summary>
    /// The definition of a user, composed of their profile along with their list of roles
    /// for a given application.
    /// </summary>
    public class User
    {
        /// <summary>
        /// The user's id within the authentication system.
        /// <remarks>This should be unique across all dependent systems.</remarks>
        /// </summary>
        public Guid UserId { get; }
        
        /// <summary>
        /// The Username that is displayed within the application.
        /// </summary>
        public string Username { get; }
        
        /// <summary>
        /// The user's "SubjectId" ....
        /// </summary>
        public string SubjectId { get; }
        
        /// <summary>
        /// The list of roles that the user is assigned for a given application.
        /// </summary>
        public HashSet<Role> Roles { get; } = new HashSet<Role>();

        
        public User(Guid userId, string username, string subjectId)
        {
            UserId = userId;
            Username = username;
            SubjectId = subjectId;
        }
    }
}