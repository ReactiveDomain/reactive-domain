using System;

namespace ReactiveDomain.Example
{
    public class GroupStopped
    {
        public Guid GroupId { get; }
        public string Name { get; }
        public Guid StoppedBy { get; }
        public long StoppedOn { get; }

        public GroupStopped(Guid groupId, string name, Guid stoppedBy, long stoppedOn) 
        {
            GroupId = groupId;
            Name = name;
            StoppedBy = stoppedBy;
            StoppedOn = stoppedOn;
        }
    }
}