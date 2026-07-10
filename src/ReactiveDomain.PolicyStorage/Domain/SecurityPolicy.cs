using System.Runtime.CompilerServices;
using ReactiveDomain.Policy.Messages;
using ReactiveDomain.Util;
[assembly: InternalsVisibleTo("ReactiveDomain.IdentityStorage")]
namespace ReactiveDomain.Policy.Domain;

internal class SecurityPolicy : ChildEntity {
	private readonly Dictionary<Guid, string> _rolesById = [];
	private readonly Dictionary<string, Guid> _rolesByName = [];
	public IReadOnlyList<Guid> Roles => _rolesById.Keys.ToList();

	public string ClientId { get; }
	public Guid AppId => Id;
	public readonly bool OneRolePerUser;
	public SecurityPolicy(
		Guid policyId,
		string clientId,
		SecuredApplication root)
		: base(policyId, root) {
		//n.b. this method is called only inside an apply handler in the root aggregate
		// so setting values is ok, but raising events is not
		// the create event is raised in the root aggregate
		Register<ApplicationMsgs.RoleCreated>(Apply);
		ClientId = clientId;
		OneRolePerUser = root.OneRolePerUser;
	}

	//Apply State only if it applies to my id
	private void Apply(ApplicationMsgs.RoleCreated @event) {
		if (@event.PolicyId == Id) {
			_rolesById.Add(@event.RoleId, @event.Name.Trim().ToLowerInvariant());
			_rolesByName.Add(@event.Name.Trim().ToLowerInvariant(), @event.RoleId);
		}
	}

	//Public methods
	/// <summary>
	/// Add a new role.
	/// </summary>
	public void AddRole(
		Guid roleId,
		string roleName) {
		if (roleId == Guid.Empty) {
			roleId = Guid.NewGuid();
		}
		Ensure.NotNullOrWhiteSpace(roleName, nameof(roleName));
		roleName = roleName.Trim();

		if (_rolesById.ContainsValue(roleName.ToLowerInvariant()) || _rolesById.ContainsKey(roleId)) {
			throw new InvalidOperationException($"Cannot add duplicate role. RoleName: {roleName} RoleId: {roleId}");
		}

		Raise(new ApplicationMsgs.RoleCreated(
			roleId,
			roleName,
			Id));
	}

	public void GrantRole(PolicyUser user, string roleName) {
		Ensure.NotNull(user, nameof(user));
		Ensure.NotNullOrWhiteSpace(roleName, nameof(roleName));
		roleName = roleName.Trim();
		Ensure.Equal(Id, user.PolicyId, nameof(user));
		if (!_rolesByName.TryGetValue(roleName.ToLowerInvariant(), out var roleId)) {
			throw new ArgumentOutOfRangeException($"Policy {ClientId} does not contain Role {roleName}");
		}
		user.AddRole(roleName, roleId);
	}

	public void RevokeRole(PolicyUser user, string roleName) {
		Ensure.NotNull(user, nameof(user));
		Ensure.NotNullOrWhiteSpace(roleName, nameof(roleName));
		roleName = roleName.Trim();
		Ensure.Equal(Id, user.PolicyId, nameof(user));
		if (!_rolesByName.TryGetValue(roleName.ToLowerInvariant(), out var roleId)) {
			throw new ArgumentOutOfRangeException($"Policy {ClientId} does not contain Role {roleName}");
		}
		user.RemoveRole(roleName, roleId);
	}
}
