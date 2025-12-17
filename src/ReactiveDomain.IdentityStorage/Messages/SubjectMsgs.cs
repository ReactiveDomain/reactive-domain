using System;
using ReactiveDomain.Messaging;

namespace ReactiveDomain.IdentityStorage.Messages;

public class SubjectMsgs {
    /// <summary>
    /// A new user login tracker was created.
    /// </summary>
    ///  <param name="SubjectId">The unique ID of the login identity subject.</param>
    ///  <param name="UserId">The unique ID of the user.</param>
    ///  <param name="SubClaim">The unique ID from the auth provider (e.g. Sub Claim) of the authenticated user.</param>
    ///  <param name="AuthProvider">The identity provider.</param>
    ///  <param name="AuthDomain">The user's domain.</param>
    public record SubjectCreated(
        Guid SubjectId,
        Guid UserId,
        string SubClaim,
        string AuthProvider,
        string AuthDomain) : Event;

    /// <summary>
    /// A user was successfully authenticated.
    /// </summary>
    /// <param name="SubjectId">The ID of the authenticated user.</param>
    /// <param name="TimeStamp">The date and time in UTC when the authentication was logged.</param>
    /// <param name="HostIpAddress">The IP address of the host asking for authentication.</param>
    /// <param name="ClientId">The ClientId of the application asking for authentication.</param>
    public record Authenticated(
        Guid SubjectId,
        DateTime TimeStamp,
        string HostIpAddress,
        string ClientId) : Event;

    /// <summary>
    /// A user was not successfully authenticated because account is locked.
    /// </summary>
    /// <param name="SubjectId">The ID of the not authenticated user.</param>
    /// <param name="TimeStamp">The date and time in UTC when the authentication attempt was logged.</param>
    /// <param name="HostIpAddress">The IP address of the host asking for authentication.</param>
    /// <param name="ClientId">The ClientId of the application asking for authentication.</param>
    public record AuthenticationFailedAccountLocked(
        Guid SubjectId,
        DateTime TimeStamp,
        string HostIpAddress,
        string ClientId) : Event;

    /// <summary>
    /// A user was not successfully authenticated because account is disabled.
    /// </summary>
    /// <param name="SubjectId">The ID of the not authenticated user.</param>
    /// <param name="TimeStamp">The date and time in UTC when the authentication attempt was logged.</param>
    /// <param name="HostIpAddress">The IP address of the host asking for authentication.</param>
    /// <param name="ClientId">The ClientId of the application asking for authentication.</param>
    public record AuthenticationFailedAccountDisabled(
        Guid SubjectId,
        DateTime TimeStamp,
        string HostIpAddress,
        string ClientId) : Event;

    /// <summary>
    /// A user was not successfully authenticated because invalid credentials were supplied.
    /// </summary>
    /// <param name="SubjectId">The ID of the not authenticated user.</param>
    /// <param name="TimeStamp">The date and time in UTC when the authentication attempt was logged.</param>
    /// <param name="HostIpAddress">The IP address of the host asking for authentication.</param>
    /// <param name="ClientId">The ClientId of the application asking for authentication.</param>
    public record AuthenticationFailedInvalidCredentials(
        Guid SubjectId,
        DateTime TimeStamp,
        string HostIpAddress,
        string ClientId) : Event;
}