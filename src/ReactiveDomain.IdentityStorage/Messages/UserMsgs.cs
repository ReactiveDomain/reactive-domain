using System;
using ReactiveDomain.Messaging;

namespace ReactiveDomain.IdentityStorage.Messages;

/// <summary>
/// Messages for the User domain.
/// </summary>
public class UserMsgs {
    /// <summary>
    /// Create a new user.
    /// </summary>
    ///  <param name="UserId">The unique ID of the user in ReactiveDomain.</param>
    /// <param name="GivenName">The user's given name. This is the first name in most cultures.</param>
    /// <param name="Surname">The user's surname or family name. This is the last name in most cultures.</param>
    /// <param name="Email">The user's email address.</param>
    /// <param name="FullName">user full name.</param>
    public record CreateUser(
        Guid UserId,
        string GivenName,
        string Surname,
        string FullName,
        string Email) : Command;

    ///  <param name="UserId">The unique ID of the user in ReactiveDomain.</param>
    public abstract record UserEvent(Guid UserId) : Event;

    /// <summary>
    /// A new user was created.
    /// </summary>
    ///  <param name="UserId">The unique ID of the user in ReactiveDomain.</param>
    public record UserCreated(Guid UserId) : UserEvent(UserId);

    /// <summary>
    /// Deactivate a user.
    /// </summary>
    ///  <param name="UserId">The unique ID of the user in ReactiveDomain.</param>
    public record Deactivate(Guid UserId) : Command;

    /// <summary>
    /// User is deactivated.
    /// </summary>
    ///  <param name="UserId">The unique ID of the user in ReactiveDomain.</param>
    public record Deactivated(Guid UserId) : UserEvent(UserId);

    /// <summary>
    /// Activate a user.
    /// </summary>
    ///  <param name="UserId">The unique ID of the user in ReactiveDomain.</param>
    public record Activate(Guid UserId) : Command;

    /// <summary>
    /// User is Activated.
    /// </summary>
    ///  <param name="UserId">The unique ID of the user in ReactiveDomain.</param>
    public record Activated(Guid UserId) : UserEvent(UserId);

    /// <summary>
    /// Update a user's optional details
    /// </summary>
    ///  <param name="UserId">The unique ID of the user in ReactiveDomain.</param>
    /// <param name="GivenName">The user's given name</param>
    /// <param name="Surname">The user's surname or family name</param>
    /// <param name="FullName">The user's full name</param>
    /// <param name="Email">The user's email address</param>
    public record UpdateUserDetails(
        Guid UserId,
        string GivenName = null,
        string Surname = null,
        string FullName = null,
        string Email = null) : Command;

    /// <summary>
    /// A user's Updated optional details
    /// </summary>
    ///  <param name="UserId">The unique ID of the user in ReactiveDomain.</param>
    /// <param name="GivenName">The user's given name</param>
    /// <param name="Surname">The user's surname or family name</param>
    /// <param name="FullName">The user's full name</param>
    /// <param name="Email">The user's email address</param>
    public record UserDetailsUpdated(
        Guid UserId,
        string GivenName,
        string Surname,
        string FullName,
        string Email) : UserEvent(UserId);

    /// <summary>
    /// Update a user's AuthDomain information.
    /// </summary>
    ///  <param name="UserId">The unique ID of the user in ReactiveDomain.</param>
    ///  <param name="SubjectId">The unique ID from the auth provider (e.g. Sub Claim) of the authenticated user.</param>
    ///  <param name="AuthProvider">The identity provider.</param>
    ///  <param name="AuthDomain">The user's domain.</param>
    ///  <param name="UserName">The username, which should be unique within the <see cref="AuthDomain"/>.</param>     
    public record MapToAuthDomain(
        Guid UserId,
        string SubjectId,
        string AuthProvider,
        string AuthDomain,
        string UserName) : Command;

    /// <summary>
    /// AuthDomain of a user was updated.
    /// </summary>
    ///  <param name="UserId">The unique ID of the user in ReactiveDomain.</param>
    ///  <param name="SubjectId">The unique ID from the auth provider (e.g. Sub Claim) of the authenticated user.</param>
    ///  <param name="AuthProvider">The identity provider.</param>
    ///  <param name="AuthDomain">The user's domain.</param>
    ///  <param name="UserName">The username, which should be unique within the <see cref="AuthDomain"/>.</param>     
    public record AuthDomainMapped(
        Guid UserId,
        string SubjectId,
        string AuthProvider,
        string AuthDomain,
        string UserName) : UserEvent(UserId);

    /// <summary>
    /// Add Client Scope to a user.
    /// </summary>
    /// <param name="UserId">The unique ID of the user in ReactiveDomain.</param>
    /// <param name="ClientScope">The client scope to which the user is being added.</param>
    public record AddClientScope(Guid UserId, string ClientScope) : Command;

    /// <summary>
    /// Client Scope Added to a user.
    /// </summary>
    /// <param name="UserId">The unique ID of the user in ReactiveDomain.</param>
    /// <param name="ClientScope">The client scope to which the user has been added.</param>
    public record ClientScopeAdded(Guid UserId, string ClientScope) : UserEvent(UserId);

    /// <summary>
    /// Remove Client Scope from a user.
    /// </summary>
    /// <param name="UserId">The unique ID of the user in ReactiveDomain.</param>
    /// <param name="ClientScope">The client scope from which the user is being removed.</param>
    public record RemoveClientScope(Guid UserId, string ClientScope) : Command;

    /// <summary>
    /// Client Scope Removed from a user.
    /// </summary>
    /// <param name="UserId">The unique ID of the user in ReactiveDomain.</param>
    /// <param name="ClientScope">The client scope from which the user has been removed.</param>
    public record ClientScopeRemoved(Guid UserId, string ClientScope) : UserEvent(UserId);
}