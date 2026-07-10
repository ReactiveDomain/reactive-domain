using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Policy.Messages;

namespace ReactiveDomain.Policy.ReadModels;

/// <summary>
/// A read model that contains a list of Policy Users. 
/// </summary>
public class PolicyUserRm :
	ReadModelBase,
	IHandle<PolicyUserMsgs.PolicyUserAdded>,
	IHandle<PolicyUserMsgs.RoleAdded>,
	IHandle<PolicyUserMsgs.RoleRemoved>,
	IHandle<PolicyUserMsgs.UserDeactivated>,
	IHandle<PolicyUserMsgs.UserReactivated> {
	/// <summary>
	/// Create a read model for getting information about Policy Users.
	/// </summary>
	public PolicyUserRm(IConfiguredConnection conn)
		: base(nameof(PolicyUserRm), conn) {
		//set handlers
		EventStream.Subscribe<PolicyUserMsgs.PolicyUserAdded>(this);
		EventStream.Subscribe<PolicyUserMsgs.RoleAdded>(this);
		EventStream.Subscribe<PolicyUserMsgs.RoleRemoved>(this);
		EventStream.Subscribe<PolicyUserMsgs.UserDeactivated>(this);
		EventStream.Subscribe<PolicyUserMsgs.UserReactivated>(this);

		//read
		long? checkpoint;
		using (var reader = conn.GetReader(nameof(PolicyUserRm), Handle)) {
			reader.Read<Domain.PolicyUser>(() => Idle);
			checkpoint = reader.Position;
		}

		//subscribe
		Start<Domain.PolicyUser>(checkpoint);
	}

	//todo: consider complex types to model this
	public readonly Dictionary<Guid, Guid> UserByPolicyUser = [];
	private readonly Dictionary<Guid, Guid> _policyByPolicyUser = [];

	public readonly Dictionary<Guid, HashSet<string>> RolesByPolicyUser = [];
	public readonly Dictionary<Guid, HashSet<Guid>> UsersByPolicy = [];
	public readonly Dictionary<Guid, HashSet<Guid>> PoliciesByUser = [];
	public readonly Dictionary<Guid, HashSet<Guid>> PolicyUsersByUserId = [];

	/// <summary>
	/// Get the policy user ID for a given combination of user ID and policy ID
	/// </summary>
	/// <param name="userId">The user ID.</param>
	/// <param name="policyId">The policy ID.</param>
	/// <param name="policyUserId">The policy user ID or Guid.Empty if none is found.</param>
	/// <returns>true if a matching user policy is found, otherwise false.</returns>
	public bool TryGetPolicyUserId(Guid userId, Guid policyId, out Guid policyUserId) {
		policyUserId = Guid.Empty;
		if (!PolicyUsersByUserId.TryGetValue(userId, out var policyUsers))
			return false;
		foreach (var policyUser in policyUsers) {
			if (!_policyByPolicyUser.TryGetValue(policyUser, out var policy) || policy != policyId)
				continue;
			policyUserId = policyUser;
			return true;
		}
		return false;
	}

	public void Handle(PolicyUserMsgs.PolicyUserAdded @event) {
		if (!PolicyUsersByUserId.TryGetValue(@event.UserId, out var policyUsers)) {
			policyUsers = [];
			PolicyUsersByUserId.Add(@event.UserId, policyUsers);
		}
		policyUsers.Add(@event.PolicyUserId);

		if (!UsersByPolicy.TryGetValue(@event.PolicyId, out var users)) {
			users = [];
			UsersByPolicy.Add(@event.PolicyId, users);
		}
		users.Add(@event.UserId);

		if (!PoliciesByUser.TryGetValue(@event.UserId, out var policies)) {
			policies = [];
			PoliciesByUser.Add(@event.UserId, policies);
		}
		policies.Add(@event.PolicyId);

		UserByPolicyUser[@event.PolicyUserId] = @event.UserId;

		_policyByPolicyUser[@event.PolicyUserId] = @event.PolicyId;

		if (!RolesByPolicyUser.TryGetValue(@event.PolicyUserId, out _)) {
			RolesByPolicyUser.Add(@event.PolicyUserId, []);
		}
	}

	public void Handle(PolicyUserMsgs.RoleAdded @event) {
		if (RolesByPolicyUser.TryGetValue(@event.PolicyUserId, out var roles)) {
			roles.Add(@event.RoleName.Trim().ToLowerInvariant());
		}
	}

	public void Handle(PolicyUserMsgs.RoleRemoved @event) {
		if (RolesByPolicyUser.TryGetValue(@event.PolicyUserId, out var roles)) {
			roles.Remove(@event.RoleName.Trim().ToLowerInvariant());
		}
	}

	public void Handle(PolicyUserMsgs.UserDeactivated @event) {
		var userId = UserByPolicyUser[@event.PolicyUserId];
		var policyId = _policyByPolicyUser[@event.PolicyUserId];

		if (UsersByPolicy.TryGetValue(policyId, out var users)) {
			users.Remove(userId);
		}
		if (PoliciesByUser.TryGetValue(userId, out var policies)) {
			policies.Remove(policyId);
		}
		if (PolicyUsersByUserId.TryGetValue(userId, out policies)) {
			policies.Remove(policyId);
		}
	}

	public void Handle(PolicyUserMsgs.UserReactivated @event) {
		var userId = UserByPolicyUser[@event.PolicyUserId];
		var policyId = _policyByPolicyUser[@event.PolicyUserId];
		if (UsersByPolicy.TryGetValue(policyId, out var users)) {
			users.Add(userId);
		}
		if (PoliciesByUser.TryGetValue(userId, out var policies)) {
			policies.Add(policyId);
		}
		if (PolicyUsersByUserId.TryGetValue(userId, out policies)) {
			policies.Add(policyId);
		}
	}
}
