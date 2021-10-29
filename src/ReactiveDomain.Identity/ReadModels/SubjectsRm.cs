using ReactiveDomain.Foundation;
using ReactiveDomain.Identity.Messages;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using System;
using System.Collections.Generic;

namespace ReactiveDomain.Identity.ReadModels
{
    public class SubjectsRm :
        ReadModelBase,
        IHandle<SubjectMsgs.SubjectCreated>
    {
        public SubjectsRm(IConfiguredConnection conn)
            : base(nameof(SubjectsRm), () => conn.GetListener(nameof(SubjectsRm)))
        {
            //set handlers
            EventStream.Subscribe<SubjectMsgs.SubjectCreated>(this);

            //read
            long? checkpoint;
            using (var reader = conn.GetReader(nameof(SubjectsRm), Handle))
            {
                reader.Read<Domain.Subject>(() => Idle);
                checkpoint = reader.Position;
            }

            //subscribe
            Start<Domain.Subject>(checkpoint, true);
        }
        public bool TryGetSubjectIdForUser(Guid userId, string provider, string domain, out Guid subjectId)
        {
            try
            {
                if (Subjects.TryGetValue($"{provider}-{domain}", out var subList))
                {
                   return subList.TryGetValue(userId, out subjectId);
                }
            }
            catch
            {
                subjectId = Guid.Empty;
                return false;
            }
            return false;
        }
        //{domain-{userId-subjectId}}
        internal readonly Dictionary<string, Dictionary<Guid, Guid>> Subjects = new Dictionary<string, Dictionary<Guid, Guid>>();
        public void Handle(SubjectMsgs.SubjectCreated @event)
        {
            if (!Subjects.TryGetValue($"{ @event.AuthProvider}-{ @event.AuthDomain}", out var subList))
            {
                subList = new Dictionary<Guid, Guid>();
                Subjects.Add($"{ @event.AuthProvider}-{ @event.AuthDomain}", subList);
            }

            if (subList.TryGetValue(@event.UserId, out var _))
            {
                subList[@event.UserId] = @event.SubjectId;
            }
            else
            {
                subList.Add(@event.UserId, @event.SubjectId);
            }
        }
    }
}
