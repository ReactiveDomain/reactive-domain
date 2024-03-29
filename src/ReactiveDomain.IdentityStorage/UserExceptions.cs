﻿using System;

namespace ReactiveDomain.IdentityStorage
{
    /// <summary>
    /// An attempt was made to add a duplicate user to the system.
    /// </summary>
    public class DuplicateUserException : Exception
    {
        /// <summary>
        /// An attempt was made to add a duplicate user to the system.
        /// </summary>
        public DuplicateUserException(Guid id, string fullName, string email)
            : base($"User {id}: {fullName}\\{email} already exists.")
        { }
    }
    
    /// <summary>
    /// Throw this exception when a user lookup returns no results.
    /// </summary>
    public class UserNotFoundException : Exception
    {
        /// <summary>
        /// Throw this exception when a user lookup returns no results.
        /// </summary>
        public UserNotFoundException(string message)
            : base(message)
        {
        }
    }

    /// <summary>
    /// Throw this exception when a user lookup returns a user but that user is deactivated.
    /// </summary>
    public class UserDeactivatedException : Exception
    {
        /// <summary>
        /// Throw this exception when a user lookup returns a user but that user is deactivated.
        /// </summary>
        public UserDeactivatedException(string message)
            : base(message)
        {
        }
    }

    public class DuplicateClientException : Exception
    {
        public DuplicateClientException(Guid id, string name)
            : base($"Client {name} with ID {id} already exists.")
        { }
    }
}
