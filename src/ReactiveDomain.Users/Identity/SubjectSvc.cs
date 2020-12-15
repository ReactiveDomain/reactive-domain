using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Util;
using System;
using System.Collections.Generic;

namespace ReactiveDomain.Users.Identity
{
    public class SubjectSvc :      
        IHandleCommand<SubjectMsgs.CreateSubject>,
        IHandleCommand<SubjectMsgs.AddRoles>,
        IHandleCommand<SubjectMsgs.RemoveRoles>

    {

        private readonly IRepository _repo;
        private HashSet<string> _knownSubjects = new HashSet<string>();

        
        public SubjectSvc(IRepository repo)            
        {
           Ensure.NotNull(repo, nameof(repo));     
            _repo = repo;
        }
        
        public CommandResponse Handle(SubjectMsgs.CreateSubject command)
        {
            //todo: detect duplicates?? or do that one level up?

            throw new NotImplementedException();
            //if (_repo.TryGetById<Application>(command.Id, out _, command)
            //    || _applicationsRm.ApplicationExists(command.Name))
            //{
            //    throw new DuplicateApplicationException(command.Name);
            //}
            //var application = new Application(
            //    command.Id,
            //    command.Name,
            //    command.OneRolePerUser,
            //    command.Roles,
            //    command.SecAdminRole,
            //    command.DefaultUser,
            //    command.DefaultDomain,
            //    command.DefaultUserRoles,
            //    command);
            //_repo.Save(application);
            //return command.Succeed();
        }
        public CommandResponse Handle(SubjectMsgs.AddRoles command)
        {
            throw new NotImplementedException();
        }
        public CommandResponse Handle(SubjectMsgs.RemoveRoles command)
        {
            throw new NotImplementedException();
        }
    }
}
