using System;
using System.Collections.Generic;
using ReactiveDomain.Messaging;

namespace ReactiveDomain.Policy.Messages
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
            public readonly Guid ApplicationId;

            /// <summary> Application name</summary>
            public readonly string Name;

            /// <summary> Application name</summary>
            public readonly string Version;
            public readonly bool OneRolePerUser;

            /// <summary>
            /// Register a new application.
            /// </summary>
            /// <param name="applicationId">The unique ID of the new application.</param>
            /// <param name="name">application name.</param>
            /// <param name="version">application version</param>
            public CreateApplication(
                Guid applicationId,
                string name,
                string version,
                bool oneRolePerUser)
            {
                ApplicationId = applicationId;
                Name = name;
                Version = version;
                OneRolePerUser = oneRolePerUser;
            }
        }

        /// <summary>
        /// Application Created.
        /// </summary>
        public class ApplicationCreated : Event
        {
            /// <summary>The unique ID of the new application.</summary>
            public readonly Guid ApplicationId;

            /// <summary> Application name</summary>
            public readonly string Name;

            /// <summary> Application name</summary>
            public readonly string ApplicationVersion;

            public readonly bool OneRolePerUser;
            /// <summary>
            /// Register a new application.
            /// </summary>
            /// <param name="applicationId">The unique ID of the new application.</param>
            /// <param name="name">application name.</param>
            /// <param name="applicationVersion">application version</param>
            public ApplicationCreated(
                Guid applicationId,
                string name,
                string applicationVersion,
                bool oneRolePerUser)
            {
                ApplicationId = applicationId;
                Name = name;
                ApplicationVersion = applicationVersion;
                OneRolePerUser = oneRolePerUser;
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

        /// <summary>
        /// Indicate that an application is no longer in use.
        /// </summary>
        public class RetireApplication : Command
        {
            /// <summary>The unique ID of the application to be retired.</summary>
            public Guid Id;

            /// <summary>
            /// Indicate that an application is no longer in use.
            /// </summary>
            /// <param name="id">The unique ID of the application to be retired.</param>
            public RetireApplication(Guid id)
            {
                Id = id;
            }
        }

        /// <summary>
        /// Indicates that an application is no longer in use.
        /// </summary>
        public class ApplicationRetired : Event
        {
            /// <summary>The unique ID of the application that has been retired.</summary>
            public Guid Id;

            /// <summary>
            /// Indicates that an application is no longer in use.
            /// </summary>
            /// <param name="id">The unique ID of the application that has been retired.</param>
            public ApplicationRetired(Guid id)
            {
                Id = id;
            }
        }

        /// <summary>
        /// Put a retired application back in use.
        /// </summary>
        public class UnretireApplication : Command
        {
            /// <summary>The unique ID of the application to be returned to use.</summary>
            public Guid Id;

            /// <summary>
            /// Put a retired application back in use.
            /// </summary>
            /// <param name="id">The unique ID of the application to be returned to use.</param>
            public UnretireApplication(Guid id)
            {
                Id = id;
            }
        }

        /// <summary>
        /// Indicates that an application has been returned to use.
        /// </summary>
        public class ApplicationUnretired : Event
        {
            /// <summary>The unique ID of the application that has been returned to use.</summary>
            public Guid Id;

            /// <summary>
            /// Indicates that an application has been returned to use.
            /// </summary>
            /// <param name="id">The unique ID of the application that has been returned to use.</param>
            public ApplicationUnretired(Guid id)
            {
                Id = id;
            }
        }

        public class CreatePolicy : Command
        {
            public readonly Guid PolicyId;
            public readonly string ClientId;
            public readonly Guid ApplicationId;

            public CreatePolicy(
                Guid policyId,
                string clientId,
                Guid applicationId)
            {
                PolicyId = policyId;
                ClientId = clientId;
                ApplicationId = applicationId;
            }
        }

        public class PolicyCreated : Event
        {
            public readonly Guid PolicyId;
            public readonly string ClientId;
            public readonly Guid ApplicationId;
            public readonly bool OneRolePerUser;

            public PolicyCreated(
                Guid policyId,
                string clientId,
                Guid applicationId,
                bool oneRolePerUser)
            {
                PolicyId = policyId;
                ClientId = clientId;
                ApplicationId = applicationId;
                OneRolePerUser = oneRolePerUser;
            }
        }

        public class STSClientDetailsAdded : Event
        {
            public readonly Guid ApplicationId;
            public readonly string ClientId;
            public readonly string[] GrantTypes;
            public readonly string EncryptedClientSecret;
            public readonly string[] AllowedScopes;
            public readonly string[] RedirectUris;
            public readonly string[] PostLogoutRedirectUris;
            public readonly string FrontChannelLogoutUri;

            public STSClientDetailsAdded(
                Guid applicationId,
                string clientId,
                string[] grantTypes,
                string encryptedClientSecret,
                string[] allowedScopes,
                string[] redirectUris,
                string[] postLogoutRedirectUris,
                string frontChannelLogoutUri)
            {
                ApplicationId = applicationId;
                ClientId = clientId;
                GrantTypes = grantTypes;
                EncryptedClientSecret = encryptedClientSecret;
                AllowedScopes = allowedScopes;
                RedirectUris = redirectUris;
                PostLogoutRedirectUris = postLogoutRedirectUris;
                FrontChannelLogoutUri = frontChannelLogoutUri;
            }
        }
        public class STSClientSecretAdded : Event
        {

            public readonly Guid ApplicationId;
            public readonly string EncryptedClientSecret;

            public STSClientSecretAdded(Guid applicationId, string encryptedClientSecret)
            {
                ApplicationId = applicationId;
                EncryptedClientSecret = encryptedClientSecret;
            }
        }
        public class STSClientSecretRemoved : Event
        {
            public readonly Guid ApplicationId;
            public readonly string EncryptedClientSecret;

            public STSClientSecretRemoved(Guid applicationId, string encryptedClientSecret)
            {
                ApplicationId = applicationId;
                EncryptedClientSecret = encryptedClientSecret;
            }
        }

    }
}
