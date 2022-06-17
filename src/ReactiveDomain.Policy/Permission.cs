using ReactiveDomain.Messaging;
using ReactiveDomain.Util;
using System;

namespace ReactiveDomain.Policy
{
    public class Permission : IComparable<Permission>, IComparable, IEquatable<Permission>
    {
        public readonly string PermissionName;
        public Type PermissionType { get; private set; }
        public readonly bool isType;

        public Permission(string nonTypeName)
        {
            Ensure.NotNullOrEmpty(nonTypeName, nameof(nonTypeName));
            PermissionName = nonTypeName;
            PermissionType = null;
            isType = false;
        }
        public Permission(Type permission)
        {
            Ensure.NotNull(permission, nameof(permission));
            Ensure.True(() => typeof(IMessage).IsAssignableFrom(permission), $"Parameter {nameof(permission)} implements IMessage ");
            PermissionName = permission.FullName;
            PermissionType = permission;
            isType = true;
        }
        public Permission(string @namespace, string typeName)
        {
            Ensure.NotNullOrEmpty(@namespace, nameof(@namespace));
            Ensure.NotNullOrEmpty(typeName, nameof(typeName));
            @namespace.TrimEnd('.');
            PermissionName = $"{@namespace}.{typeName}";
            PermissionType = null;
            isType = true;
        }
        public bool TryResovleType()
        {
            if (!isType) { return false; }
            if (PermissionType != null) { return true; }
            try
            {
                var type = MessageHierarchy.GetTypeByFullName(PermissionName);
                if (type != null)
                {
                    PermissionType = type;
                    return true;
                }
            }
            catch { /*ignore and return false */ }
            return false;
        }
        #region IEquatable<T> Implementation
        public bool Equals(Permission other)
        {
            if (other is null) return false;
            return string.Equals(PermissionName, other.PermissionName);
        }

        public override bool Equals(object obj) => Equals(obj as Permission);
        public override int GetHashCode()
        {
            // Use `unchecked` so if results overflows it is truncated          
            unchecked
            {
                // Computing hashCode from https://aaronstannard.com/overriding-equality-in-dotnet/
                var hashCode = 13;
                hashCode = ComputeHash(hashCode, PermissionName.GetHashCode());
                return hashCode;
            }
        }
        // == and != 
        public static bool operator ==(Permission x, Permission y) => x?.Equals(y) ?? y is null;
        public static bool operator !=(Permission x, Permission y) => !(x?.Equals(y) ?? y is null);
        public int ComputeHash(int currentHash, int value) => (currentHash * 397) ^ value;
        #endregion IEquatable<T> Implementation

        #region IComparable<T> Implementation
        public int CompareTo(object other) => CompareTo(other as Permission);
        public int CompareTo(Permission other)
        {
            if (other == null) return 1;
            return string.CompareOrdinal(PermissionName, other.PermissionName);
        }
        // >, <, >=, <= from source 2
        public static bool operator >(Permission op1, Permission op2) => op1.CompareTo(op2) == 1;
        public static bool operator <(Permission op1, Permission op2) => op1.CompareTo(op2) == -1;
        public static bool operator >=(Permission op1, Permission op2) => op1.CompareTo(op2) >= 0;
        public static bool operator <=(Permission op1, Permission op2) => op1.CompareTo(op2) <= 0;
        #endregion IComparable<T> Implementation
    }
}
