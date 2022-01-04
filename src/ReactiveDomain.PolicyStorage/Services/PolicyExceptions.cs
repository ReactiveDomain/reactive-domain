using System;

namespace ReactiveDomain.Policy
{
    /// <summary>
    /// An attempt was made to add a duplicate application to the system.
    /// </summary>
    public class DuplicateApplicationException : Exception
    {
        /// <summary>
        /// An attempt was made to add a duplicate application to the system.
        /// </summary>
        public DuplicateApplicationException(string appName, string securityModelVersion)
            : base($"Application {appName} with version {securityModelVersion} already exists.")
        { }
    }
}
