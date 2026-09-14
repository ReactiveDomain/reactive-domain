using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using ReactiveDomain.Util;

namespace ReactiveDomain.Policy;

public class Policy : IComparable<Policy>, IComparable, IEquatable<Policy> {
	public readonly string PolicyName;
	public readonly bool SingleRole;

	public ReadOnlyCollection<string> RoleNames => _roles.Select(r => r.RoleName).ToList().AsReadOnly();
	private readonly HashSet<Role> _roles = [];
	private readonly Dictionary<string, Role> _rolesByName = [];
	public Policy(string policyName, bool singleRole = false, params Role[]? roles) {
		Ensure.NotNullOrEmpty(policyName, nameof(policyName));
		PolicyName = policyName;
		SingleRole = singleRole;
		if (roles == null) { return; }
		foreach (var role in roles) {
			_roles.Add(role);
			_rolesByName.Add(role.RoleName, role);
		}
	}

	public bool TryGetRole(string roleName, [NotNullWhen(true)] out Role? role) {
		return _rolesByName.TryGetValue(roleName, out role);
	}

	#region IEquatable<T> Implementation
	public bool Equals(Policy? other) {
		if (other is null)
			return false;
		return string.Equals(PolicyName, other.PolicyName, StringComparison.OrdinalIgnoreCase);
	}

	public override bool Equals(object? obj) => Equals(obj as Policy);
	public override int GetHashCode() {
		// Use `unchecked` so if results overflows it is truncated          
		unchecked {
			// Computing hashCode from https://aaronstannard.com/overriding-equality-in-dotnet/
			var hashCode = 13;
			hashCode = ComputeHash(hashCode, PolicyName.GetHashCode());
			return hashCode;
		}
	}
	// == and != 
	public static bool operator ==(Policy? x, Policy? y) => x?.Equals(y) ?? y is null;
	public static bool operator !=(Policy? x, Policy? y) => !(x?.Equals(y) ?? y is null);
	public int ComputeHash(int currentHash, int value) => (currentHash * 397) ^ value;
	#endregion IEquatable<T> Implementation

	#region IComparable<T> Implementation
	public int CompareTo(object? other) => CompareTo(other as Policy);
	public int CompareTo(Policy? other) => other == null ? 1 : string.CompareOrdinal(PolicyName, other.PolicyName);
	// >, <, >=, <= from source 2
	public static bool operator >(Policy op1, Policy op2) => op1.CompareTo(op2) == 1;
	public static bool operator <(Policy op1, Policy op2) => op1.CompareTo(op2) == -1;
	public static bool operator >=(Policy op1, Policy op2) => op1.CompareTo(op2) >= 0;
	public static bool operator <=(Policy op1, Policy op2) => op1.CompareTo(op2) <= 0;
	#endregion IComparable<T> Implementation
}
