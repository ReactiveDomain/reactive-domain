﻿using System;
using System.Collections.Generic;
using ReactiveDomain.Foundation;
using ReactiveDomain.IdentityStorage.Domain;
using ReactiveDomain.IdentityStorage.Messages;
using ReactiveDomain.Messaging.Bus;

namespace ReactiveDomain.IdentityStorage.ReadModels
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
                reader.Read<Subject>(() => Idle);
                checkpoint = reader.Position;
            }

            //subscribe
            Start<Subject>(checkpoint, true);
        }
        public string GetDomainCategory(string provider, string domain)
        {
            return $"{provider}-{domain.ToLowerInvariant()}";
        }
        public bool TryGetSubjectIdForUser(Guid userId, string provider, string domain, out Guid subjectId)
        {
            try
            {
                if (SubjectsByUserId.TryGetValue(GetDomainCategory(provider, domain), out var subList))
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
        public bool TryGetSubjectIdForPrinciple(IPrinciple principle, out Guid subjectId)
        {
            try
            {

                if (SubjectsBySubClaim.TryGetValue(GetDomainCategory(principle.Provider, principle.Domain), out var subList))
                {
                    return subList.TryGetValue(principle.SId, out subjectId);
                }
            }
            catch
            {
                subjectId = Guid.Empty;
                return false;
            }
            return false;
        }

        //{domainCategory-{userId-subjectId}}
        internal readonly Dictionary<string, Dictionary<Guid, Guid>> SubjectsByUserId = new Dictionary<string, Dictionary<Guid, Guid>>();
        //{domainCategory-{sid-subjectId}}
        internal readonly Dictionary<string, Dictionary<string, Guid>> SubjectsBySubClaim = new Dictionary<string, Dictionary<string, Guid>>();
        public void Handle(SubjectMsgs.SubjectCreated @event)
        {
            if (!SubjectsByUserId.TryGetValue(GetDomainCategory(@event.AuthProvider, @event.AuthDomain), out var subList))
            {
                subList = new Dictionary<Guid, Guid>();
                SubjectsByUserId.Add(GetDomainCategory(@event.AuthProvider, @event.AuthDomain), subList);
            }

            if (subList.TryGetValue(@event.UserId, out _))
            {
                subList[@event.UserId] = @event.SubjectId;
            }
            else
            {
                subList.Add(@event.UserId, @event.SubjectId);
            }
            if (!SubjectsBySubClaim.TryGetValue(GetDomainCategory(@event.AuthProvider, @event.AuthDomain), out var subjectsByClaim))
            {
                subjectsByClaim = new Dictionary<string, Guid>();
                SubjectsBySubClaim.Add(GetDomainCategory(@event.AuthProvider, @event.AuthDomain), subjectsByClaim);
            }

            if (subjectsByClaim.TryGetValue(@event.SubClaim, out _))
            {
                subjectsByClaim[@event.SubClaim] = @event.SubjectId;
            }
            else
            {
                subjectsByClaim.Add(@event.SubClaim, @event.SubjectId);
            }
        }
    }
}
