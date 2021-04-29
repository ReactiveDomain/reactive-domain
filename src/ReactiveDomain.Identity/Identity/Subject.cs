using System;
using System.Collections.Generic;
using System.Linq;
using ReactiveDomain.Util;
namespace ReactiveDomain.Users.Identity
{



    /// <summary>
    /// Aggregate for a Application.
    /// </summary>
    public class Subject : AggregateRoot
    {

        private HashSet<string> _roles = new HashSet<string>();

        private Subject()
        {
            RegisterEvents();
        }

        private void RegisterEvents()
        {
            Register<SubjectMsgs.SubjectCreated>(Apply);
            Register<SubjectMsgs.RolesAdded>(Apply);
            Register<SubjectMsgs.RolesRemoved>(Apply);
        }

        private void Apply(SubjectMsgs.SubjectCreated evt)
        {
            Id = evt.SubjectId;

        }
        private void Apply(SubjectMsgs.RolesAdded evt)
        {
            _roles.UnionWith(evt.Roles);
        }
        private void Apply(SubjectMsgs.RolesRemoved evt)
        {
            _roles.ExceptWith(evt.Roles);
        }
               
        public Subject(
            Guid subjectId,
            string ProviderSubClaim)
            : this()
        {
            Ensure.NotEmptyGuid(subjectId, nameof(subjectId));
            Ensure.NotNullOrEmpty(ProviderSubClaim, nameof(ProviderSubClaim));

            Raise(new SubjectMsgs.SubjectCreated(subjectId, ProviderSubClaim));
        }
        public void AddRoles(
            HashSet<string> roles)
        {
            Ensure.NotNullOrEmpty(roles, nameof(roles));

            if (roles.IsSubsetOf(_roles))
            {
                return; //idempotent success
            }
            roles.ExceptWith(_roles);
            Raise(new SubjectMsgs.RolesAdded(Id, roles.ToArray()));
        }
        public void RemoveRoles(
           HashSet<string> roles)
        {
            Ensure.NotNullOrEmpty(roles, nameof(roles));
            if (!roles.Overlaps(_roles))
            {
                return; //idempotent success
            }
            roles.ExceptWith(_roles);

             Raise(new SubjectMsgs.RolesRemoved(Id, roles.ToArray()));
        }
    }
}


