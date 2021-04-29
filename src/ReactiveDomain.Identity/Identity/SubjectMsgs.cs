using ReactiveDomain.Messaging;
using System;

namespace ReactiveDomain.Users.Identity
{
    public class SubjectMsgs
    {
        public class CreateSubject : Command
        {           
            public readonly Guid SubjectId;            
            public readonly string ProviderSubClaim;

            public CreateSubject(
                Guid subjectId,
                string providerSubClaim)
            {
                SubjectId = subjectId;
                ProviderSubClaim = providerSubClaim;
            }
        }

      
         public class SubjectCreated : Event
        {           
            public readonly Guid SubjectId;            
            public readonly string ProviderSubClaim;

            public SubjectCreated(
                Guid subjectId,
                string providerSubClaim)
            {
                SubjectId = subjectId;
                ProviderSubClaim = providerSubClaim;
            }
        }
        
        public class AddRoles : Command
        {
            public readonly Guid SubjectId;     
            public readonly string[] Roles;
            public AddRoles(
                Guid subjectId,
                string[] roles)
            {
                SubjectId = subjectId;               
                Roles = roles;
            }
        }
          public class RolesAdded : Event
        {
            public readonly Guid SubjectId;     
            public readonly string[] Roles;
            public RolesAdded(
                Guid subjectId,
                string[] roles)
            {
                SubjectId = subjectId;               
                Roles = roles;
            }
        }
        public class RemoveRoles : Command
        {
            public readonly Guid SubjectId;     
            public readonly string[] Roles;
            public RemoveRoles(
                Guid subjectId,
                string[] roles)
            {
                SubjectId = subjectId;               
                Roles = roles;
            }
        }
          public class RolesRemoved : Event
        {
            public readonly Guid SubjectId;     
            public readonly string[] Roles;
            public RolesRemoved(
                Guid subjectId,
                string[] roles)
            {
                SubjectId = subjectId;               
                Roles = roles;
            }
        }
    }
}
