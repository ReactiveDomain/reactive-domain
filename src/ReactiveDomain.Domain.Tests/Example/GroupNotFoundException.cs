using System;

namespace ReactiveDomain.Domain.Tests.Example
{
    public class GroupNotFoundException : Exception
    {
        public GroupNotFoundException(GroupIdentifier identifier)
            : base($"The group with identifier {identifier.ToGuid():N} could not be found.")
        {
        }
    }
}