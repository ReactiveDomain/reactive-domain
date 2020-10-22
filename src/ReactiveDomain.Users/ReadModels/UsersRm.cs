using System;
using System.Collections.Generic;
using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Users.Domain;
using ReactiveDomain.Users.Messages;

namespace ReactiveDomain.Users.ReadModels
{
    public class UsersRm :
        ReadModelBase,
        IHandle<UserMsgs.UserEvent>
    {
        public readonly Dictionary<string, Guid> UserIdsBySubjectAtDomain = new Dictionary<string, Guid>();
        public readonly Dictionary<Guid, UserDTO> UsersById = new Dictionary<Guid, UserDTO>();

        public UsersRm(IConfiguredConnection conn) : base(nameof(UsersRm), () => conn.GetListener(nameof(UsersRm)))
        {
            long position;
            using (var reader = conn.GetReader(nameof(UsersRm)))
            {
                reader.EventStream.Subscribe<UserMsgs.UserEvent>(this);
                reader.Read<User>();
                position = reader.Position ?? StreamPosition.Start;
            }
            EventStream.Subscribe<UserMsgs.UserEvent>(this);
            Start<User>(checkpoint: position);
        }


        public bool HasUser(string subjectId, string authDomain, out Guid userId)
        {
            var subject = $"{subjectId}@{authDomain}";
            return UserIdsBySubjectAtDomain.TryGetValue(subject, out userId);
        }

        public void Handle(UserMsgs.UserEvent @event)
        {
            if (UsersById.TryGetValue(@event.UserId, out var user))
            {
                user.Handle((dynamic)@event);
            }

            switch (@event)
            {
                case UserMsgs.UserCreated created:
                    UsersById.Add(created.UserId, new UserDTO(created.UserId));
                    break;
                case UserMsgs.AuthDomainMapped mapped:
                    var subject = $"{mapped.SubjectId}@{mapped.AuthDomain}";
                    if (UserIdsBySubjectAtDomain.ContainsKey(subject))
                    {
                        UserIdsBySubjectAtDomain[subject] = @event.UserId;
                    }
                    else
                    {
                        UserIdsBySubjectAtDomain.Add(subject, @event.UserId);
                    }
                    break;
            }
        }
    }
}
