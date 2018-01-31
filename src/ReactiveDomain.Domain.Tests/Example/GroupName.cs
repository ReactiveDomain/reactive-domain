using System;

namespace ReactiveDomain.Example
{
    public struct GroupName
    {
        private readonly string _value;

        public GroupName(string value)
        {
            if (string.IsNullOrEmpty(value))
                throw new ArgumentException("The group name can not be empty.");
            if (value.Length > 256)
                throw new ArgumentException("The group name must not exceed 256 characters.");

            _value = value;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is GroupName && Equals((GroupName)obj);
        }
        public bool Equals(GroupName other) => _value.Equals(other._value);
        public override int GetHashCode() => _value.GetHashCode();
        public static bool operator ==(GroupName left, GroupName right) => left.Equals(right);
        public static bool operator !=(GroupName left, GroupName right) => !left.Equals(right);
        public override string ToString() => _value;
    }
}