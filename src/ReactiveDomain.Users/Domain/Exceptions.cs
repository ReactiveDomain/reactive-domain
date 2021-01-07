using System;

namespace ReactiveDomain.Users.Domain
{
    /// <summary>
    /// An attempt was made to add a duplicate user to the system.
    /// </summary>
    public class DuplicateUserException : Exception
    {
        /// <summary>
        /// An attempt was made to add a duplicate user to the system.
        /// </summary>
        public DuplicateUserException(string authProvider, string authDomain, string userName)
            : base($"User {authDomain}\\{userName} with provider {authProvider} already exists.")
        { }
    }
    /// <summary>
    /// An attempt was made to add a duplicate role to the system.
    /// </summary>
    public class DuplicateRoleException : Exception
    {
        /// <summary>
        /// An attempt was made to add a duplicate role to the system.
        /// </summary>
        public DuplicateRoleException(string roleName, string application)
            : base($"Role {roleName} for {application} already exists.")
        { }
    }
    /// <summary>
    /// An error occured trying to ingest Elbe configuration data.
    /// </summary>
    public class ConfigurationException : Exception
    {
        /// <summary>
        /// An error occured trying to ingest Elbe configuration data.
        /// </summary>
        public ConfigurationException(string message, string fileName)
            : base($"{message}\nFile: {fileName}")
        { }
    }
    /// <summary>
    /// An attempt was made to add a duplicate application to the system.
    /// </summary>
    public class DuplicateApplicationException : Exception
    {
        /// <summary>
        /// An attempt was made to add a duplicate application to the system.
        /// </summary>
        public DuplicateApplicationException(string application)
            : base($"Application {application} already exists.")
        { }
    }

}
