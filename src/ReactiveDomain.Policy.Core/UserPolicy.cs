using ReactiveDomain.IdentityStorage.ReadModels;
using ReactiveDomain.Util;

namespace ReactiveDomain.Policy;

public class UserPolicy {
	public static UserPolicy EmptyPolicy() {
		return new UserPolicy();
	}

	private UserPolicy() {
		UserId = Guid.Empty;
	}

	public UserDTO? User { get; }
	public Guid UserId { get; }
	public IReadOnlyList<Role> Roles => _roles.ToList().AsReadOnly();
	private readonly HashSet<Role> _roles = [];
	private readonly HashSet<string> _roleNames = [];
	private readonly HashSet<Permission> _permissions = [];
	private readonly HashSet<Type> _permissionTypes = [];
	private readonly HashSet<string> _permissionNames = [];

	public UserPolicy(UserDTO user, HashSet<Role> grantedRoles) {
		Ensure.NotEmptyGuid(user.UserId, nameof(user));
		Ensure.NotNull(user, nameof(user));
		Ensure.NotNull(grantedRoles, nameof(grantedRoles));
		User = user;
		UserId = user.UserId;
		foreach (var role in grantedRoles) {
			AddRole(role);
		}
	}

	public void AddRole(Role role) {
		_roles.Add(role);
		_roleNames.Add(role.RoleName.Trim().ToLowerInvariant());
		foreach (var permission in role.Permissions) {
			_permissions.Add(permission);
			_permissionNames.Add(permission.PermissionName);
			if (permission.TryResolveType()) {
				_permissionTypes.Add(permission.PermissionType!);
			}
		}
	}

	public bool HasRole(string? roleName) => !string.IsNullOrWhiteSpace(roleName) &&
											 _roleNames.Contains(roleName.Trim().ToLowerInvariant());

	public bool HasRole(Role? role) => role != null && _roles.Contains(role);

	public bool HasPermission(string? permissionName) =>
		!string.IsNullOrWhiteSpace(permissionName) && _permissionNames.Contains(permissionName);

	public bool HasPermission(Permission? permission) => permission != null && _permissions.Contains(permission);

	public bool HasPermission(Type? permissionType) =>
		permissionType != null && _permissionTypes.Contains(permissionType);
}
