using System;
using ReactiveDomain.Messaging;

namespace ReactiveDomain.IdentityStorage.Messages;

public class ClientMsgs {
    public record CreateClient(
        Guid ClientId,
        Guid ApplicationId,
        string ClientName,
        string[] RedirectUris,
        string[] PostLogoutRedirectUris,
        string FrontChannelLogoutUri,
        string EncryptedClientSecret)
        : Command;

    public record ClientCreated(
        Guid ClientId,
        Guid ApplicationId,
        string ClientName,
        string[] GrantTypes,
        string[] AllowedScopes,
        string[] RedirectUris,
        string[] PostLogoutRedirectUris,
        string FrontChannelLogoutUri)
        : Event;

    public record AddClientSecret(Guid ClientId, string EncryptedClientSecret) : Command;

    public record ClientSecretAdded(Guid ClientId, string EncryptedClientSecret) : Event;

    public record RemoveClientSecret(Guid ClientId, string EncryptedClientSecret) : Command;

    public record ClientSecretRemoved(Guid ClientId, string EncryptedClientSecret) : Event;
}