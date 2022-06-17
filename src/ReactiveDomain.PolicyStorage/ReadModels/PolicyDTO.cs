using System;
using System.Text;
using DynamicData;
using ReactiveDomain.Policy.Messages;

namespace ReactiveDomain.Policy.ReadModels
{
    public class PolicyDTO    {
        public Guid PolicyId { get; }
        public Guid ApplicationId { get; }
        public string Name { get; }
        public bool OneRolePerUser { get; }
        public readonly ISourceCache<RoleDTO, Guid> Roles = new SourceCache<RoleDTO, Guid>(t => t.Id);
        public readonly ISourceCache<RoleDTO, string> RolesByName = new SourceCache<RoleDTO, string>(t => t.Name);
        public readonly ISourceCache<PolicyUserDTO, Guid> Users = new SourceCache<PolicyUserDTO, Guid>(t => t.PolicyUserId);
        public PolicyDTO(Guid policyId, Guid applicationId, string name, bool oneRolePerUser)
        {
            PolicyId = policyId;
            ApplicationId = applicationId;
            Name = name;
            OneRolePerUser = oneRolePerUser;
        }

        public PolicyDTO(ApplicationMsgs.PolicyCreated @event)
        {
            PolicyId = @event.PolicyId;
            ApplicationId = @event.ApplicationId;
            Name = @event.ClientId;
            OneRolePerUser = @event.OneRolePerUser;
        }
         public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Policy Name:{Name}");
            sb.AppendLine($"Policy Id:{PolicyId}");
            sb.AppendLine($"App Id:{ApplicationId}");
            sb.AppendLine($"IsSingleRole:{OneRolePerUser}");
            sb.AppendLine("Roles:");
            foreach (var role in Roles.Items)
            {
                sb.AppendLine($"\t{role.Name}");
            }
            return sb.ToString();
        }
    }
}
