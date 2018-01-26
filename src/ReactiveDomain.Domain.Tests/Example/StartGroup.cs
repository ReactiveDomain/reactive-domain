using System;

namespace ReactiveDomain.Example
{
    public class StartGroup
    {
        public Guid GroupId { get; }
        public string Name { get; }
        public Guid AdministratorId { get; }

        public StartGroup(Guid groupId, string name, Guid administratorId)
        {
            GroupId = groupId;
            Name = name;
            AdministratorId = administratorId;
        }
    }
}