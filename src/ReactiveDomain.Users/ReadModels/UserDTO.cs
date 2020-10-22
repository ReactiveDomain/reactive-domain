using System;
using System.Collections.Generic;
using System.Linq;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Users.Messages;

namespace ReactiveDomain.Users.ReadModels
{
    public class UserDTO :
                IHandle<UserMsgs.Deactivated>,
                IHandle<UserMsgs.Activated>,
                IHandle<UserMsgs.UserDetailsUpdated>,
                IHandle<UserMsgs.AuthDomainMapped>,
                IHandle<UserMsgs.ClientScopeAdded>,
                IHandle<UserMsgs.ClientScopeRemoved>

    {
        public Guid UserId { private set; get; }
        public bool Active { private set; get; }
        public string FullName { private set; get; }
        public string GivenName { private set; get; }
        public string Surname { private set; get; }
        public string Email { private set; get; }
        public string SubjectId { private set; get; }
        public string AuthProvider { private set; get; }
        public string AuthDomain { private set; get; }
        public string UserName { private set; get; }
        private readonly HashSet<string> _scopes = new HashSet<string>();
        public IReadOnlyList<string> Scopes => _scopes.ToList();

        public UserDTO(Guid userId)
        {
            UserId = userId;
        }

        public void Handle(UserMsgs.Deactivated @event)
        {
            Active = false;
        }
        public void Handle(UserMsgs.Activated @event)
        {
            Active = true;
        }
        public void Handle(UserMsgs.UserDetailsUpdated @event)
        {
            FullName = @event.FullName;
            GivenName = @event.GivenName;
            Surname = @event.Surname;
            Email = @event.Email;
        }
        public void Handle(UserMsgs.AuthDomainMapped @event)
        {
            SubjectId = @event.SubjectId;
            AuthProvider = @event.AuthProvider;
            AuthDomain = @event.AuthDomain;
            UserName = @event.UserName;
        }
        public void Handle(UserMsgs.ClientScopeAdded @event)
        {
            _scopes.Add(@event.ClientScope);
        }
        public void Handle(UserMsgs.ClientScopeRemoved @event)
        {
            _scopes.Remove(@event.ClientScope);
        }
    }
}
