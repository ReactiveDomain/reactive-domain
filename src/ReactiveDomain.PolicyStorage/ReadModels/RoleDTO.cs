using ReactiveDomain.Policy.Messages;
using System;

namespace ReactiveDomain.Policy.ReadModels
{
    public class RoleDTO
    {
        public Guid Id { get; }
        public Guid PolicyId { get; }
        public string Name { get; }
        public RoleDTO(Guid id, Guid policyId, string name)
        {
            Id = id;
            PolicyId = policyId;
            Name = name.Trim().ToLowerInvariant();
        }

        public RoleDTO(ApplicationMsgs.RoleCreated @event)
        {
            Id = @event.RoleId;
            PolicyId = @event.PolicyId;
            Name = @event.Name.Trim().ToLowerInvariant();
        }
    }
}
