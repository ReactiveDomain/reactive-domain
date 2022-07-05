﻿using System;
using System.Collections.Generic;
using DynamicData;
using ReactiveDomain.Foundation;
using ReactiveDomain.IdentityStorage.Domain;
using ReactiveDomain.IdentityStorage.Messages;
using ReactiveDomain.Messaging.Bus;

namespace ReactiveDomain.IdentityStorage.ReadModels
{
    public class UsersRm :
        ReadModelBase,
        IHandle<UserMsgs.UserEvent>
    {
        public readonly Dictionary<string, Guid> UserIdsBySubjectAtDomain = new Dictionary<string, Guid>();
        public readonly Dictionary<Guid, UserDTO> UsersById = new Dictionary<Guid, UserDTO>();

        public IConnectableCache<UserDTO, Guid> AllUsers => _allUsers;
        private readonly SourceCache<UserDTO, Guid> _allUsers = new SourceCache<UserDTO, Guid>(x => x.UserId);

        private readonly List<Guid> _userIds = new List<Guid>();
        public UsersRm(IConfiguredConnection conn) : base(nameof(UsersRm), () => conn.GetListener(nameof(UsersRm)))
        {
            long? position;
            EventStream.Subscribe<UserMsgs.UserEvent>(this);
            using (var reader = conn.GetReader(nameof(UsersRm), Handle))
            {               
                reader.Read<User>(()=> Idle);
                position = reader.Position;               
            }

            Start<User>(checkpoint: position, blockUntilLive: true);
        }

        public List<Guid> GetUserIds()
        {
            lock (_userIds)
            {
                return new List<Guid>(_userIds);
            }
        }
        public bool HasUser(string subjectId, string authDomain, out Guid userId)
        {
            var subject = $"{subjectId}@{authDomain.ToLowerInvariant()}";
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
                    lock (_userIds)
                    {
                        _userIds.Add(@event.UserId);
                    }
                    var userDto = new UserDTO(created.UserId);
                    UsersById.Add(created.UserId, userDto);
                    _allUsers.AddOrUpdate(userDto);
                    break;
                case UserMsgs.AuthDomainMapped mapped:
                    var subject = $"{mapped.SubjectId}@{mapped.AuthDomain.ToLowerInvariant()}";
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
