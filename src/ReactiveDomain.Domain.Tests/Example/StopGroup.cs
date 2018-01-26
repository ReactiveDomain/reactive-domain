using System;

namespace ReactiveDomain.Example
{
    public class StopGroup
    {
        public Guid GroupId { get; }
        public Guid AdministratorId { get; }

        public StopGroup(Guid groupId, Guid administratorId)
        {
            GroupId = groupId;
            AdministratorId = administratorId;
        }
    }
}