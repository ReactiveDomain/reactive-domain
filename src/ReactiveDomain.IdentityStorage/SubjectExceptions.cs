using System;

namespace ReactiveDomain.IdentityStorage
{
    /// <summary>
    /// An attempt was made to add a duplicate subject to the system.
    /// </summary>
    public class DuplicateSubjectException : Exception
    {
        /// <summary>
        /// An attempt was made to add a duplicate subject to the system.
        /// </summary>
        public DuplicateSubjectException(string authProvider, string authDomain, string userName)
            : base($"User {authDomain}\\{userName} with provider {authProvider} already exists.")
        { }
    }
}
