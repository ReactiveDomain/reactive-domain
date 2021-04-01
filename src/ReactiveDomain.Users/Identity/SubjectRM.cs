using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Users.Policy;

namespace ReactiveDomain.Users.Identity
{
    /// <summary>
    /// A read model that contains a list of existing Subjects. 
    /// </summary>
    public class SubjectRM :
        ReadModelBase,
        IHandle<SubjectMsgs.SubjectCreated>,
        IHandle<SubjectMsgs.RolesAdded>,
        IHandle<SubjectMsgs.RolesRemoved>
    {
        private readonly List<SubjectDTO> _subjects = new List<SubjectDTO>();

        private IReadOnlyList<SubjectDTO> Subject { get { return _subjects; } }

        /// <summary>
        /// Create a read model for getting information about subjecs.
        /// </summary>
        public SubjectRM(Func<IListener> getListener)
            : base(nameof(SubjectRM), getListener)
        {

            EventStream.Subscribe<SubjectMsgs.SubjectCreated>(this);
            EventStream.Subscribe<SubjectMsgs.RolesAdded>(this);
            EventStream.Subscribe<SubjectMsgs.RolesRemoved>(this);

            Start<Subject>(blockUntilLive: true);
        }


        public void Handle(SubjectMsgs.SubjectCreated @event)
        {
            _subjects.Add(new SubjectDTO(
                                    @event.SubjectId,
                                    @event.ProviderSubClaim));
        }
        public void Handle(SubjectMsgs.RolesAdded @event)
        {
            var subject = _subjects.FirstOrDefault(c => c.SubjectId == @event.SubjectId);
            if (subject == null)
            {
                //todo: log this, it should never happen
                return;
            }
            subject.Roles.UnionWith(@event.Roles);
        }
        public void Handle(SubjectMsgs.RolesRemoved @event)
        {
            var subject = _subjects.FirstOrDefault(c => c.SubjectId == @event.SubjectId);
            if (subject == null)
            {
                //todo: log this, it should never happen
                return;
            }
            subject.Roles.ExceptWith(@event.Roles);
        }
        
        // rebuild this to pull a shell of a `User` type.
        public bool TryGetSubject(string ProviderSubClaim, out SubjectDTO subject)
        {
            subject = _subjects.FirstOrDefault(x => string.CompareOrdinal(x.ProviderSubClaim, ProviderSubClaim) == 0);
            return subject != null;
        }

        public bool TryGetUser(ClaimsPrincipal principal, out User user)
        {
            // find `sub` claim within principal.  If not there, throw error.
            // attempt to locate SubjectDTO using sub claim value.
            // if not there, return false, keep user null
            
            // if there, then build shell of User.
            user = new User(Guid.Empty, "", "", "");
            user.Roles.UnionWith(new Role[]{}); // pull this from SubjectDTO found in #76
            return user != null;
        }
    }
}
