using System;
using System.Collections.Generic;
using ReactiveDomain.Messaging;

namespace ReactiveDomain.Users.Messages
{
    /// <summary>
    /// Messages for the Application domain.
    /// </summary>
    public class ApplicationMsgs
    {
        /// <summary>
        /// Create a new application aggregate.
        /// </summary>
        public class CreateApplication : Command
        {
            /// <summary>The unique ID of the new application.</summary>
            public readonly Guid Id;
            /// <summary> Application name</summary>
            public readonly string Name;
            /// <summary> Application name</summary>
            public readonly string Version;
            
            /// <summary>
            /// Register a new application.
            /// </summary>
            /// <param name="id">The unique ID of the new application.</param>
            /// <param name="name">application name.</param>
            /// <param name="version">application version</param>
            public CreateApplication(
                Guid id,
                string name,
                string version)
            {
                Id = id;
                Name = name;
                Version = version;
            }
        }

        /// <summary>
        /// Application Created.
        /// </summary>
        public class ApplicationCreated  : Event
        {
            /// <summary>The unique ID of the new application.</summary>
            public readonly Guid ApplicationId;
            /// <summary> Application name</summary>
            public readonly string Name;
            /// <summary> Application name</summary>
            public readonly string ApplicationVersion;
            
            /// <summary>
            /// Register a new application.
            /// </summary>
            /// <param name="applicationId">The unique ID of the new application.</param>
            /// <param name="name">application name.</param>
            /// <param name="version">application version</param>
            public ApplicationCreated(
                Guid applicationId,
                string name,
                string version)
            {
                ApplicationId = applicationId;
                Name = name;
                ApplicationVersion = version;
            }
        }

        /// <summary>
        /// Configure a new application.
        /// </summary>
        public class ConfigureApplication : Command
        {
            /// <summary>The unique ID of the new ConfigureApplication command.</summary>
            public readonly Guid Id;
            /// <summary> Application name</summary>
            public readonly string Name;
            /// <summary> Application version</summary>
            public readonly string Version;
            /// <summary> does this application demand one role per user?</summary>
            public bool OneRolePerUser { get; }
            /// <summary> List of roles available for this application </summary>
            public List<string> Roles { get; }
            /// <summary> SecAdminRole name, this must exists in Roles list</summary>
            public string SecAdminRole { get; }
            /// <summary> Default user name</summary>
            public readonly string DefaultUser;
            /// <summary> Default user's domain name</summary>
            public readonly string DefaultDomain;
            /// <summary> Roles which default user would be assigned.This list must be a subset of Roles</summary>
            public List<string> DefaultUserRoles { get; }
            /// <summary> Authentication provider, for local and domains it should be AD and for external, it should be the name</summary>
            public string AuthProvider { get; }

            /// <summary>
            /// Register a new application.
            /// </summary>
            /// <param name="id">The unique ID of the new application.</param>
            /// <param name="name">application name</param>
            /// <param name="version">application version</param>
            /// <param name="oneRolePerUser">does this application demand one role per user?</param>
            /// <param name="roles">List of roles available for this application</param>
            /// <param name="secAdminRole">SecAdminRole name, this must exists in Roles list</param>
            /// <param name="defaultUser">Default user name</param>
            /// <param name="defaultDomain">Default user's domain name</param>
            /// <param name="defaultUserRoles">Roles which default user would be assigned.This list must be a subset of Roles</param>
            /// <param name="authProvider">Authentication provider name or AD for local/domain users</param>
            public ConfigureApplication(
                Guid id,
                string name,
                string version,
                bool oneRolePerUser,
                List<string> roles,
                string secAdminRole,
                string defaultUser,
                string defaultDomain,
                List<string> defaultUserRoles,
                string authProvider)
            {
                Id = id;
                Name = name;
                Version = version;
                OneRolePerUser = oneRolePerUser;
                SecAdminRole = secAdminRole;
                DefaultUser = defaultUser;
                Roles = roles;
                DefaultDomain = defaultDomain;
                DefaultUserRoles = defaultUserRoles;
                AuthProvider = authProvider;
            }
        }
    }
}
