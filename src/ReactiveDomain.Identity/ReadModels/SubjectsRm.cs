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
                reader.Read<Domain.Subject>();
                checkpoint = reader.Position;
                while (!Idle) { };
            }

            //subscribe
            Start<Domain.Subject>(checkpoint, true);
        }
        public Dictionary<Guid, Guid> SubjectIdByUserId = new Dictionary<Guid, Guid>();
        public void Handle(SubjectMsgs.SubjectCreated @event)
        {
            if (SubjectIdByUserId.ContainsKey(@event.UserId))
            {
                SubjectIdByUserId[@event.UserId] = @event.SubjectId;
            }
            else
            {
                SubjectIdByUserId.Add(@event.UserId, @event.SubjectId);
            }
        }
    }
}
