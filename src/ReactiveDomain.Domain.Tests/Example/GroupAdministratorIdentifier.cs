using System;

namespace ReactiveDomain.Domain.Tests.Example
{
    public struct GroupAdministratorIdentifier : IEquatable<GroupAdministratorIdentifier>
    {
        private readonly Guid value;

        public GroupAdministratorIdentifier(Guid value)
        {
            if (value == Guid.Empty)
                throw new ArgumentException("A group administrator identifier cannot be empty.", nameof(value));
            this.value = value;
        }

        public override bool Equals(object obj) => obj is GroupAdministratorIdentifier && Equals((GroupAdministratorIdentifier)obj);
        public bool Equals(GroupAdministratorIdentifier other) => this.value.Equals(other.value);
        public override int GetHashCode() => this.value.GetHashCode();
        public static bool operator ==(GroupAdministratorIdentifier left, GroupAdministratorIdentifier right) => left.Equals(right);
        public static bool operator !=(GroupAdministratorIdentifier left, GroupAdministratorIdentifier right) => !left.Equals(right);
        public Guid ToGuid() => this.value;
        public static implicit operator Guid(GroupAdministratorIdentifier instance) => instance.ToGuid();
    }
}