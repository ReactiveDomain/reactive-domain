using ReactiveDomain.Policy.Messages;
using System;
using System.Text;

namespace ReactiveDomain.Policy.ReadModels
{
    public class ApplicationDTO
    {
        public Guid ApplicationId { get; }
        public string Name { get; }
        public Version SecurityModelVersion { get; }
        public bool OneRolePerUser { get; }
        public ApplicationDTO(Guid applicationId, string name, Version securityModelVersion, bool oneRolePerUser)
        {
            ApplicationId = applicationId;
            Name = name;
            SecurityModelVersion = securityModelVersion;
            OneRolePerUser = oneRolePerUser;
        }
        public ApplicationDTO(ApplicationMsgs.ApplicationCreated @event)
        {
            ApplicationId = @event.ApplicationId;
            Name = @event.Name;
            SecurityModelVersion = Version.Parse(@event.SecurityModelVersion);
            OneRolePerUser = @event.OneRolePerUser;
        }
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"App Name:{Name}");
            sb.AppendLine($"App Id:{ApplicationId}");
            sb.AppendLine($"Ver:{SecurityModelVersion}");
            sb.AppendLine($"IsSingleRole:{OneRolePerUser}");
            return sb.ToString();
        }
    }
}
