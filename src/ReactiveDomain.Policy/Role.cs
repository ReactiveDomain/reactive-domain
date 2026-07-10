using ReactiveDomain.Util;

namespace ReactiveDomain.Policy;

public class Role : IComparable<Role>, IComparable, IEquatable<Role> {
	public readonly string RoleName;
	public readonly HashSet<Permission> Permissions = [];

	public Role(string roleName, params Permission[]? permissions) :
		this(roleName) {
		if (permissions == null) { return; }
		foreach (var permission in permissions) {
			Permissions.Add(permission);
		}
	}

	public Role(string roleName) {
		Ensure.NotNullOrEmpty(roleName.Trim(), nameof(roleName));
		RoleName = roleName.Trim();
	}

	#region IEquatable<T> Implementation
	public bool Equals(Role? other) {
		return other is not null && string.Equals(RoleName.ToLowerInvariant(), other.RoleName.ToLowerInvariant());
	}

	public override bool Equals(object? obj) => Equals(obj as Role);
	public override int GetHashCode() {
		// Use `unchecked` so if results overflows it is truncated          
		unchecked {
			// Computing hashCode from https://aaronstannard.com/overriding-equality-in-dotnet/
			var hashCode = 13;
			hashCode = ComputeHash(hashCode, RoleName.GetHashCode());
			return hashCode;
		}
	}
	// == and != 
	public static bool operator ==(Role? x, Role? y) => x?.Equals(y) ?? y is null;
	public static bool operator !=(Role? x, Role? y) => !(x?.Equals(y) ?? y is null);
	public int ComputeHash(int currentHash, int value) => (currentHash * 397) ^ value;
	#endregion IEquatable<T> Implementation

	#region IComparable<T> Implementation
	public int CompareTo(object? other) => CompareTo(other as Role);
	public int CompareTo(Role? other) => other == null ? 1 : string.CompareOrdinal(RoleName, other.RoleName);
	// >, <, >=, <= from source 2
	public static bool operator >(Role? op1, Role? op2) => op1?.CompareTo(op2) == 1;
	public static bool operator <(Role? op1, Role? op2) => op1?.CompareTo(op2) == -1;
	public static bool operator >=(Role? op1, Role? op2) => op1?.CompareTo(op2) >= 0;
	public static bool operator <=(Role? op1, Role? op2) => op1?.CompareTo(op2) <= 0;
	#endregion IComparable<T> Implementation
}
