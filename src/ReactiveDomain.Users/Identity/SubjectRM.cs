using System;
using System.Collections.Generic;
using System.Linq;
using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Users.Identity;

namespace Elbe.Domain
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
        public bool TryGetSubject(string ProviderSubClaim, out SubjectDTO subject)
        {
            subject = _subjects.FirstOrDefault(x => string.CompareOrdinal(x.ProviderSubClaim, ProviderSubClaim) == 0);
            return subject != null;
        }
    }
}
