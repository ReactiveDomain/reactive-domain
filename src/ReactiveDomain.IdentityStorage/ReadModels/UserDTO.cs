using ReactiveDomain.IdentityStorage.Messages;
using ReactiveDomain.Messaging.Bus;

namespace ReactiveDomain.IdentityStorage.ReadModels;

public class UserDTO :
	IHandle<UserMsgs.Deactivated>,
	IHandle<UserMsgs.Activated>,
	IHandle<UserMsgs.UserDetailsUpdated>,
	IHandle<UserMsgs.AuthDomainMapped>,
	IHandle<UserMsgs.ClientScopeAdded>,
	IHandle<UserMsgs.ClientScopeRemoved> {
	public Guid UserId { get; private set; }
	public bool Active { get; private set; }
	public string? FullName { get; private set; }
	public string? GivenName { get; private set; }
	public string? Surname { get; private set; }
	public string? Email { get; private set; }
	public string? SubjectId { get; private set; }
	public string? AuthProvider { get; private set; }
	public string? AuthDomain { get; private set; }
	public string? UserName { get; private set; }
	private readonly HashSet<string> _scopes = [];
	public IReadOnlyList<string> Scopes => _scopes.ToList();

	public UserDTO(Guid userId) {
		UserId = userId;
	}

	public void Handle(UserMsgs.Deactivated @event) {
		Active = false;
	}

	public void Handle(UserMsgs.Activated @event) {
		Active = true;
	}

	public void Handle(UserMsgs.UserDetailsUpdated @event) {
		FullName = @event.FullName;
		GivenName = @event.GivenName;
		Surname = @event.Surname;
		Email = @event.Email;
	}

	public void Handle(UserMsgs.AuthDomainMapped @event) {
		SubjectId = @event.SubjectId;
		AuthProvider = @event.AuthProvider;
		AuthDomain = @event.AuthDomain;
		UserName = @event.UserName;
	}

	public void Handle(UserMsgs.ClientScopeAdded @event) {
		_scopes.Add(@event.ClientScope);
	}

	public void Handle(UserMsgs.ClientScopeRemoved @event) {
		_scopes.Remove(@event.ClientScope);
	}
}
