using System;
using System.Collections.Generic;
using System.DirectoryServices.AccountManagement;
using System.Security.Claims;
using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Messaging.Messages;
using ReactiveDomain.Users.Domain;
using ReactiveDomain.Users.Messages;
using ReactiveDomain.Users.ReadModels;

namespace ReactiveDomain.Users.Services
{
    public class UserStore
    {
        private readonly UserSvc _userSvc;
        private readonly UsersRm _usersRm;
        private readonly SubjectsRm _subjectsRm;

        private readonly ICorrelatedRepository _repo;
        private readonly IConfiguredConnection _conn;

        public UserStore(IConfiguredConnection conn)
        {
            _conn = conn;
            _userSvc = new UserSvc(_conn.GetRepository(), new Dispatcher("bus-to-nowhere"));
            _usersRm = new UsersRm(_conn);
            _subjectsRm = new SubjectsRm(_conn);
            _repo = _conn.GetCorrelatedRepository();
        }

        public Guid UpdateUserInfo(UserPrincipal retrievedUser, string domain, string authProvider, Guid? proposedUserId = null)
        {
            //existing User in ES, update the user details if they have changed
            if (_usersRm.HasUser(retrievedUser.Sid.Value, domain, out Guid userId))
            {
                _userSvc.Handle(
                    MessageBuilder.New(
                        () => new UserMsgs.UpdateUserDetails(
                            userId,
                            retrievedUser.GivenName,
                            retrievedUser.Surname,
                            retrievedUser.DisplayName,
                            retrievedUser.EmailAddress)));
            }
            else //User not present in ES Add User and Subject for Audit Tracking
            {
                userId = AddUser(retrievedUser, domain, authProvider, proposedUserId ?? Guid.NewGuid() , out var _);
            }
            return userId;
        }

        public Guid AddUser(UserPrincipal retrievedUser, string domain, string authProvider, Guid userId, out Guid subjectId)
        {            
            subjectId = Guid.NewGuid();
            var createMsg =
                MessageBuilder.New(
                    () => new UserMsgs.CreateUser(
                        userId,
                        retrievedUser.GivenName,
                        retrievedUser.Surname,
                        retrievedUser.DisplayName,
                        retrievedUser.EmailAddress));
            _userSvc.Handle(createMsg);
            _userSvc.Handle(
                MessageBuilder
                    .From(createMsg)
                    .Build(() =>
                        new UserMsgs.MapToAuthDomain(
                        userId,
                        retrievedUser.Sid.Value,
                        authProvider,
                        domain,
                        retrievedUser.SamAccountName)));
            var subject = new Subject(
                subjectId,
                userId,
                retrievedUser.Sid.Value,
                authProvider,
                domain,
                createMsg);
            _repo.Save(subject);
            return userId;
        }

        //return the allowed client scopes the user has access to 
        public List<Claim> GetAdditionalClaims(Guid userId)
        {
            var claimList = new List<Claim>();
            claimList.Add(new Claim("rd-userid", userId.ToString()));
            if (_usersRm.UsersById.ContainsKey(userId))
            {
                foreach (string scope in _usersRm.UsersById[userId].Scopes)
                {
                    claimList.Add(new Claim("policy-access", scope));
                }
            }
            return claimList;
        }

        //Add events to the Subject for tracking the authentication status results
        public void UserAuthenticated(UserPrincipal retrievedUser, string domain, string authProvider, string hostIpAddress, string clientId)
        {
            if (_usersRm.HasUser(retrievedUser.Sid.Value, domain, out Guid userId) &&
                _subjectsRm.TryGetSubjectIdForUser(userId, authProvider, domain, out var subId))
            {
                var subject = _repo.GetById<Subject>(subId, new CorrelatedRoot());
                subject.Authenticated(hostIpAddress, clientId);
                _repo.Save(subject);
            }
        }
        public void UserProvidedInvalidCredentials(UserPrincipal retrievedUser, string domain, string authProvider, string hostIpAddress, string clientId)
        {
            if (_usersRm.HasUser(retrievedUser.Sid.Value, domain, out Guid userId) &&
                   _subjectsRm.TryGetSubjectIdForUser(userId, authProvider, domain, out var subId))
            {
                var subject = _repo.GetById<Subject>(subId, new CorrelatedRoot());
                subject.NotAuthenticatedInvalidCredentials(hostIpAddress, clientId);
                _repo.Save(subject);
            }
        }
        public void UserAccountLocked(UserPrincipal retrievedUser, string domain, string authProvider, string hostIpAddress, string clientId)
        {
            if (_usersRm.HasUser(retrievedUser.Sid.Value, domain, out Guid userId) &&
                  _subjectsRm.TryGetSubjectIdForUser(userId, authProvider, domain, out var subId))
            {
                var subject = _repo.GetById<Subject>(subId, new CorrelatedRoot());
                subject.NotAuthenticatedAccountLocked(hostIpAddress, clientId);
                _repo.Save(subject);
            }
        }
        public void UserAccountDisabled(UserPrincipal retrievedUser, string domain, string authProvider, string hostIpAddress, string clientId)
        {
            if (_usersRm.HasUser(retrievedUser.Sid.Value, domain, out Guid userId) &&
                _subjectsRm.TryGetSubjectIdForUser(userId, authProvider, domain, out var subId))
            {
                var subject = _repo.GetById<Subject>(subId, new CorrelatedRoot());
                subject.NotAuthenticatedAccountDisabled(hostIpAddress, clientId);
                _repo.Save(subject);
            }
        }
    }
}
