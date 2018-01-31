using System;

namespace ReactiveDomain.Domain.Tests.Example
{
    public struct GroupIdentifier : IEquatable<GroupIdentifier>
    {
        private readonly Guid value;

        public GroupIdentifier(Guid value)
        {
            if (value == Guid.Empty)
                throw new ArgumentException("A group identifier cannot be empty.", nameof(value));
            this.value = value;
        }

        public override bool Equals(object obj) => obj is GroupIdentifier && Equals((GroupIdentifier)obj);
        public bool Equals(GroupIdentifier other) => this.value.Equals(other.value);
        public override int GetHashCode() => this.value.GetHashCode();
        public static bool operator ==(GroupIdentifier left, GroupIdentifier right) => left.Equals(right);
        public static bool operator !=(GroupIdentifier left, GroupIdentifier right) => !left.Equals(right);
        public Guid ToGuid() => this.value;
        public static implicit operator Guid(GroupIdentifier instance) => instance.ToGuid();
        public static implicit operator StreamName(GroupIdentifier instance) => new StreamName("groups-" + instance.ToGuid().ToString("N"));
    }
}