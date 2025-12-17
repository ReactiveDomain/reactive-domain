using System;
using ReactiveDomain.Messaging;

namespace ReactiveDomain.Policy.Messages;

/// <summary>
/// Messages for the Application domain.
/// </summary>
public class ApplicationMsgs {
    /// <summary>
    /// Create a new application aggregate.
    /// </summary>
    /// <param name="ApplicationId">The unique ID of the new application.</param>
    /// <param name="DefaultPolicyId">The unique ID of the default policy that is to be created along with the secured application.</param>
    /// <param name="Name">Application name.</param>
    /// <param name="SecurityModelVersion">The version of security model that applies to this application.</param>
    /// <param name="OneRolePerUser">If true, each user may only have a single role.</param>
    public record CreateApplication(
        Guid ApplicationId,
        Guid DefaultPolicyId,
        string Name,
        string SecurityModelVersion,
        bool OneRolePerUser) : Command;

    /// <summary>
    /// A new application has been registered.
    /// </summary>
    /// <param name="ApplicationId">The unique ID of the new application.</param>
    /// <param name="Name">The application name.</param>
    /// <param name="SecurityModelVersion">The version of the application's security model.</param>
    /// <param name="OneRolePerUser">If true, each user may only have a single role.</param>
    public record ApplicationCreated(
        Guid ApplicationId,
        string Name,
        string SecurityModelVersion,
        bool OneRolePerUser) : Event;

    /// <summary>
    /// Indicate that an application is no longer in use.
    /// </summary>
    /// <param name="Id">The unique ID of the application to be retired.</param>
    public record RetireApplication(Guid Id) : Command;

    /// <summary>
    /// Indicates that an application is no longer in use.
    /// </summary>
    /// <param name="Id">The unique ID of the application that has been retired.</param>
    public record ApplicationRetired(Guid Id) : Event;

    /// <summary>
    /// Put a retired application back in use.
    /// </summary>
    /// <param name="Id">The unique ID of the application to be returned to use.</param>
    public record UnretireApplication(Guid Id) : Command;

    /// <summary>
    /// Indicates that an application has been returned to use.
    /// </summary>
    /// <param name="Id">The unique ID of the application that has been returned to use.</param>
    public record ApplicationUnretired(Guid Id) : Event;

    /// <summary>
    /// Creates a new policy.
    /// </summary>
    /// <param name="PolicyId">The unique ID of the policy.</param>
    /// <param name="ClientId">The unique ID of the client to which the policy applies.</param>
    /// <param name="ApplicationId">The unique ID of the application to which the policy applies.</param>
    public record CreatePolicy(
        Guid PolicyId,
        string ClientId,
        Guid ApplicationId)
        : Command;

    /// <summary>
    /// A new policy is created.
    /// </summary>
    /// <param name="PolicyId">The unique ID of the policy.</param>
    /// <param name="ClientId">The unique ID of the client to which the policy applies.</param>
    /// <param name="ApplicationId">The unique ID of the application to which the policy applies.</param>
    public record PolicyCreated(
        Guid PolicyId,
        string ClientId,
        Guid ApplicationId,
        bool OneRolePerUser)
        : Event;

    /// <summary>
    /// Create a new Role.
    /// </summary>
    /// <param name="RoleId">The unique ID of the new role.</param>
    /// <param name="Name">The name of the role.</param>
    /// <param name="PolicyId">The policy this role applies to.</param>
    /// <param name="AppId">The application this role applies to.</param>
    public record CreateRole(
        Guid? RoleId,
        string Name,
        Guid PolicyId,
        Guid AppId) : Command;

    /// <summary>
    /// A new role was created.
    /// </summary>
    public record RoleCreated : Event {
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
            Guid policyId) {
            RoleId = roleId;
            Name = name;
            PolicyId = policyId;
        }
    }
    /// <summary>
    /// Add a client registration for the token server
    /// </summary>
    /// <param name="ClientId">The unique ID of the added client.</param>
    /// <param name="ApplicationId">The Application ID.</param>
    public record AddClientRegistration(
        Guid ClientId,
        Guid ApplicationId) : Command;

    /// <summary>
    /// Client registration for the token server Added
    /// </summary>
    /// <param name="ClientId">The unique ID of the added client.</param>
    /// <param name="ApplicationId">The Application ID.</param>
    public record ClientRegistrationAdded(
        Guid ClientId,
        Guid ApplicationId) : Event;
}