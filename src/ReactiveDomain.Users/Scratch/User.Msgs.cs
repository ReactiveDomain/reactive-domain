using System;

using ReactiveDomain.Messaging;

namespace ReactiveDomain.Users.Scratch
{
    public static class UserMsgs
    {
        public class PolicyAdded : Event
        {
            public readonly Guid UserId;
            public readonly Guid PolicyId;

            public PolicyAdded(Guid userId, Guid policyId)
            {
                UserId = userId;
                PolicyId = policyId;
            }
        }

        public class Deactivated : Event
        {
            public readonly Guid UserId;

            public Deactivated(Guid userId)
            {
                UserId = userId;
            }
        }

        public class Reactivated : Event
        {
            public readonly Guid UserId;

            public Reactivated(Guid userId)
            {
                UserId = userId;
            }
        }

        public class GivenNameUpdated : Event
        {
            public readonly Guid UserId;
            public readonly string GivenName;

            public GivenNameUpdated(Guid userId, string givenName)
            {
                UserId = userId;
                GivenName = givenName;
            }
        }

        public class SurnameUpdated : Event
        {
            public readonly Guid UserId;
            public readonly string Surname;

            public SurnameUpdated(Guid userId, string surname)
            {
                UserId = userId;
                Surname = surname;
            }
        }

        public class FullNameUpdated : Event
        {
            public readonly Guid UserId;
            public readonly string FullName;

            public FullNameUpdated(Guid userId, string fullName)
            {
                UserId = userId;
                FullName = fullName;
            }
        }

        public class UsernameUpdated : Event
        {
            public readonly Guid UserId;
            public readonly string Username;

            public UsernameUpdated(Guid userId, string username)
            {
                UserId = userId;
                Username = username;
            }
        }

        public class AuthorityRegistered : Event
        {
            public readonly Guid UserId;
            public readonly string Authority;
            public readonly string Domain;

            public AuthorityRegistered(Guid userId, string authority, string domain)
            {
                UserId = userId;
                Authority = authority;
                Domain = domain;
            }
        }

        public class AuthorityDeregistered : Event
        {
            public readonly Guid UserId;
            public readonly string Authority;
            public readonly string Domain;

            public AuthorityDeregistered(Guid userId, string authority, string domain)
            {
                UserId = userId;
                Authority = authority;
                Domain = domain;
            }
        }

        public class EmailUpdated : Event
        {
            public readonly Guid UserId;
            public readonly string Email;

            public EmailUpdated(Guid userId, string email)
            {
                UserId = userId;
                Email = email;
            }
        }

        public class AliasRegistered
        {
            public readonly Guid UserId;
            public readonly string Authority;
            public readonly string Domain;
            
            public AliasRegistered(Guid userId, string authority, string domain)
            {
                UserId = userId;
                Authority = authority;
                Domain = domain;
            }
        }

        public class AliasRemoved
        {
            public readonly Guid UserId;
            public readonly string Authority;
            public readonly string Domain;
            
            public AliasRemoved(Guid userId, string authority, string domain)
            {
                UserId = userId;
                Authority = authority;
                Domain = domain;
            }
        }
    }
}