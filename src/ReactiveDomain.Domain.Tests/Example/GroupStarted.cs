using System;

namespace ReactiveDomain.Example
{
    public class GroupStarted
    {
        public Guid GroupId { get; }
        public string Name { get; }
        public Guid StartedBy { get; }
        public long StartedOn { get; }

        public GroupStarted(Guid groupId, string name, Guid startedBy, long startedOn) 
        {
            GroupId = groupId;
            Name = name;
            StartedBy = startedBy;
            StartedOn = startedOn;
        }
    }
}