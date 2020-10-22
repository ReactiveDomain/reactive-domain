using System;
using System.Collections.Generic;
using System.DirectoryServices.AccountManagement;
using System.Security.Claims;
using ReactiveDomain.Foundation;
using ReactiveDomain.Identity.Domain;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Messages;
using ReactiveDomain.Users.Messages;
using ReactiveDomain.Users.ReadModels;
using ReactiveDomain.Users.Sevices;

namespace ReactiveDomain.Identity.Stores
{
    public class UserStore
    {
        private readonly UserSvc _userSvc;
        private readonly UsersRm _usersRm;
        private readonly ICorrelatedRepository _repo;

        public UserStore(IConfiguredConnection conn)
        {
            _userSvc = new UserSvc(conn.GetRepository());
            _usersRm = new UsersRm(conn);
            _repo = conn.GetCorrelatedRepository();
        }

        public void UpdateUserInfo(UserPrincipal retrievedUser, string domain, string authProvider)
        {
            //existing User in ES, update the user details if they have changed
            if (_usersRm.HasUser(retrievedUser.Sid.Value, domain, out Guid userId))
            {
                _userSvc.Handle(
                    MessageBuilder.New(()=>
                    new UserMsgs.UpdateUserDetails(
                    userId,
                    retrievedUser.GivenName,
                    retrievedUser.Surname,
                    retrievedUser.DisplayName,
                    retrievedUser.EmailAddress)));
            }
            else //User not present in ES Add User and Subject for Audit Tracking
            {
                userId = Guid.NewGuid();
                var createMsg = 
                    MessageBuilder.New(
                        ()=> new UserMsgs.CreateUser(
                            userId,
                            retrievedUser.GivenName,
                            retrievedUser.Surname,
                            retrievedUser.DisplayName,
                            retrievedUser.EmailAddress));
                _userSvc.Handle(createMsg);
                _userSvc.Handle(
                    MessageBuilder
                        .From(createMsg)
                        .Build(()=>
                            new UserMsgs.MapToAuthDomain(
                            userId,
                            retrievedUser.Sid.Value,
                            authProvider,
                            domain,
                            retrievedUser.SamAccountName)));
                var subject = new Subject(
                    userId,
                    retrievedUser.Sid.Value,
                    authProvider,
                    domain,
                    createMsg);
                _repo.Save(subject);
            }
        }
        //return the allowed client scopes the user has access to 
        public List<Claim> GetAccessClaims(UserPrincipal retrievedUser, string domain)

        {
            var claims = new List<Claim>();
            if (_usersRm.HasUser(retrievedUser.Sid.Value, domain, out Guid userId))
            {
                var user = _usersRm.UsersById[userId];
                foreach (var clientScope in user.Scopes)
                {
                    claims.Add(new Claim("policy-access", clientScope));
                }
            }
            return claims;
        }

        //Add events to the Subject for tracking the authentication status results
        public void UserAuthenticated(UserPrincipal retrievedUser, string domain, string hostIpAddress)
        {
            if (_usersRm.HasUser(retrievedUser.Sid.Value, domain, out Guid userId))
            {
                var subject = _repo.GetById<Subject>(userId, new CorrelatedRoot());
                subject.Authenticated(hostIpAddress);
                _repo.Save(subject);
            }
        }
        public void UserProvidedInvalidCredentials(UserPrincipal retrievedUser, string domain, string hostIpAddress)
        {
            if (_usersRm.HasUser(retrievedUser.Sid.Value, domain, out Guid userId))
            {
                var subject = _repo.GetById<Subject>(userId, new CorrelatedRoot());
                subject.NotAuthenticatedInvalidCredentials(hostIpAddress);
                _repo.Save(subject);
            }

        }
        public void UserAccountLocked(UserPrincipal retrievedUser, string domain, string hostIpAddress)
        {
            if (_usersRm.HasUser(retrievedUser.Sid.Value, domain, out Guid userId))
            {
                var subject = _repo.GetById<Subject>(userId, new CorrelatedRoot());
                subject.NotAuthenticatedAccountLocked(hostIpAddress);
                _repo.Save(subject);
            }

        }
        public void UserAccountDisabled(UserPrincipal retrievedUser, string domain, string hostIpAddress)
        {
            if (_usersRm.HasUser(retrievedUser.Sid.Value, domain, out Guid userId))
            {
                var subject = _repo.GetById<Subject>(userId, new CorrelatedRoot());
                subject.NotAuthenticatedAccountDisabled(hostIpAddress);
                _repo.Save(subject);
            }

        }
       
    }
}
