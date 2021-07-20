using ReactiveDomain.Policy.Messages;
using System;

namespace ReactiveDomain.Policy.ReadModels
{
    public class ApplicationDTO
    {
        public Guid ApplicationId { get; }
        public string Name { get; }
        public bool OneRolePerUser { get; }
        public ApplicationDTO( Guid applicationId, string name, bool oneRolePerUser)
        {
            ApplicationId = applicationId;
            Name = name;
            OneRolePerUser = oneRolePerUser;
        }
        public ApplicationDTO(ApplicationMsgs.ApplicationCreated @event)
        {
            ApplicationId = @event.ApplicationId;
            Name = @event.Name;
            OneRolePerUser = @event.OneRolePerUser;
        }
    }
}
