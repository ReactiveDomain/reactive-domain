using System;
using ReactiveDomain.Messaging;

namespace ReactiveDomain.Policy.Messages;

public class PolicyUserMsgs {
    public record AddPolicyUser(Guid PolicyUserId, Guid UserId, Guid PolicyId, Guid ApplicationId) : Command;
    public record PolicyUserAdded(Guid PolicyUserId, Guid UserId, Guid PolicyId, bool OneRolePerUser) : Event;

    public record AddRole(Guid PolicyUserId, string RoleName, Guid RoleId) : Command;
    public record RoleAdded(Guid PolicyUserId, Guid RoleId, string RoleName) : Event;

    public record RemoveRole(Guid PolicyUserId, string RoleName, Guid RoleId) : Command;

    public record RoleRemoved(Guid PolicyUserId, Guid RoleId, string RoleName) : Event;
    public record DeactivateUser(Guid PolicyUserId) : Command;

    public record UserDeactivated(Guid PolicyUserId) : Event;
    public record ReactivateUser(Guid PolicyUserId) : Command;

    public record UserReactivated(Guid PolicyUserId) : Event;
}