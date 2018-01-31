using System;

namespace ReactiveDomain.Domain.Tests.Example
{
    public class GroupDeletedException : Exception
    {
        public GroupDeletedException(GroupIdentifier identifier)
            : base($"The group with identifier {identifier.ToGuid():N} was deleted.")
        {
        }
    }
}