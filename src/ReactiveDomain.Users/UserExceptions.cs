using System;

namespace ReactiveDomain.Users
{
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
}
