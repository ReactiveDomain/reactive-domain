using System;
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
            /// <summary>The unique ID of the default policy that is to be created along with the secured application.</summary>
            public readonly Guid DefaultPolicyId;
            /// <summary>Application name</summary>
            public readonly string Name;
            /// <summary>Security model version</summary>
            public readonly string SecurityModelVersion;
            /// <summary>If true, each user may only have a single role.</summary>
            public readonly bool OneRolePerUser;

            /// <summary>
            /// Register a new application.
            /// </summary>
            /// <param name="applicationId">The unique ID of the new application.</param>
            /// <param name="defaultPolicyId">The unique ID of the default policy that is to be created along with the secured application.</param>
            /// <param name="name">Application name.</param>
            /// <param name="securityModelVersion">Security model version</param>
            /// <param name="oneRolePerUser">If true, each user may only have a single role.</param>
            public CreateApplication(
                Guid applicationId,
                Guid defaultPolicyId,
                string name,
                string securityModelVersion,
                bool oneRolePerUser)
            {
                ApplicationId = applicationId;
                DefaultPolicyId = defaultPolicyId;
                Name = name;
                SecurityModelVersion = securityModelVersion;
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
            public readonly string SecurityModelVersion;

            /// <summary>If true, each user may only have a single role.</summary>
            public readonly bool OneRolePerUser;

            /// <summary>
            /// Register a new application.
            /// </summary>
            /// <param name="applicationId">The unique ID of the new application.</param>
            /// <param name="name">The application name.</param>
            /// <param name="securityModelVersion">The version of the application's security model.</param>
            /// <param name="oneRolePerUser">If true, each user may only have a single role.</param>
            public ApplicationCreated(
                Guid applicationId,
                string name,
                string securityModelVersion,
                bool oneRolePerUser)
            {
                ApplicationId = applicationId;
                Name = name;
                SecurityModelVersion = securityModelVersion;
                OneRolePerUser = oneRolePerUser;
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
        /// <summary>
        /// Create a new Role.
        /// </summary>
        public class CreateRole : Command
        {
            /// <summary>The unique ID of the new role.</summary>
            public readonly Guid? RoleId;
            /// <summary>The name of the role.</summary>
            public readonly string Name;
            /// <summary>The policy this role applies to.</summary>
            public readonly Guid PolicyId;
            /// <summary>The application this role applies to.</summary>
            public readonly Guid AppId;

            /// <summary>
            /// Create a new Role.
            /// </summary>
            public CreateRole(
                Guid? roleId,
                string name,
                Guid policyId,
                Guid appId)
            {
                RoleId = roleId;
                Name = name;
                PolicyId = policyId;
                AppId = appId;
            }

        }

        /// <summary>
        /// A new role was created.
        /// </summary>
        public class RoleCreated : Event
        {
            /// <summary>The unique ID of the new role.</summary>
            public readonly Guid RoleId;
            /// <summary>The name of the role.</summary>
            public readonly string Name;
            /// <summary>The policy this role applies to.</summary>
            public readonly Guid PolicyId;

            /// <summary>
            /// A new role was created.
            /// </summary>
            public RoleCreated(
                Guid roleId,
                string name,
                Guid policyId)
            {
                RoleId = roleId;
                Name = name;
                PolicyId = policyId;
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
