using System;
using ReactiveDomain.Messaging;
using ReactiveDomain.Users.Domain.Aggregates;
using ReactiveDomain.Users.Messages;
using ReactiveDomain.Users.Tests.Helpers;
using Xunit;

namespace ReactiveDomain.Users.Tests
{
    [Collection("UserDomainTests")]
    public sealed class UserAggregateTests
    {
        private readonly ICorrelatedMessage _command = MessageBuilder.New(() => new TestMessages.RootCommand());

        private readonly Guid _id = Guid.NewGuid();
        private readonly string _userSidFromAuthProvider = Guid.NewGuid().ToString();
        private const string AuthProvider = Constants.AuthenticationProviderAD;
        private const string AuthDomain = "Perkinelmernet";
        private const string UserName = "jsmith";
        private const string GivenName = "John";
        private const string Surname = "Smith";
        private const string FullName = "John Smith";
        private const string Email = "john.smith@perkinelmer.com";
        private const string HostIPAddress = "127.0.0.1";
        private const string GivenNameUpdate = "John Update";
        private const string SurnameUpdate = "Smith Update";
        private const string FullNameUpdate = "John Smith Update";
        private const string EmailUpdate = "john.smithUpdate@perkinelmer.com";
        private readonly string UserSidFromAuthProviderUpdate = Guid.NewGuid().ToString() + "Update";
        private readonly string AuthDomainUpdate = "Perkinelmernet Update";
        private readonly string UserNameUpdate = "jsmith Update";

        [Fact]
        public void can_create_new_user()
        {
            var user = new IdentityAgg(
                            _id,
                            _userSidFromAuthProvider,
                            AuthProvider,
                            AuthDomain,
                            UserName,
                            FullName,
                            GivenName,
                            Surname,
                            Email,
                            _command);
            var events = user.TakeEvents();
            Assert.Collection(
                            events,
                            e =>
                            {
                                if (e is PolicyUserMsgs.UserCreated created)
                                {
                                    Assert.Equal(_id, created.Id);
                                    Assert.Equal(AuthProvider, created.AuthProvider);
                                    Assert.Equal(AuthDomain, created.AuthDomain);
                                    Assert.Equal(UserName, created.UserName);
                                    Assert.Equal(GivenName, created.GivenName);
                                    Assert.Equal(Surname, created.Surname);
                                    Assert.Equal(Email, created.Email);
                                }
                                else
                                {
                                    throw new Exception("wrong event.");
                                }
                            });
        }

        [Fact]
        public void can_create_new_user_with_empty_email()
        {
            string emptyEmail = string.Empty;
            var user = new IdentityAgg(
                _id,
                _userSidFromAuthProvider,
                AuthProvider,
                AuthDomain,
                UserName,
                FullName,
                GivenName,
                Surname,
                emptyEmail,
                _command);
            var events = user.TakeEvents();
            Assert.Collection(
                events,
                e =>
                {
                    if (e is PolicyUserMsgs.UserCreated created)
                    {
                        Assert.Equal(_id, created.Id);
                        Assert.Equal(AuthProvider, created.AuthProvider);
                        Assert.Equal(AuthDomain, created.AuthDomain);
                        Assert.Equal(UserName, created.UserName);
                        Assert.Equal(GivenName, created.GivenName);
                        Assert.Equal(Surname, created.Surname);
                        Assert.Equal(emptyEmail, created.Email);
                    }
                    else
                    {
                        throw new Exception("wrong event.");
                    }
                });
        }
        [Fact]
        public void can_create_new_user_with_empty_given_name()
        {
            string emptyGivenName = string.Empty;
            var user = new IdentityAgg(
                _id,
                _userSidFromAuthProvider,
                AuthProvider,
                AuthDomain,
                UserName,
                FullName,
                emptyGivenName,
                Surname,
                Email,
                _command);
            var events = user.TakeEvents();
            Assert.Collection(
                events,
                e =>
                {
                    if (e is PolicyUserMsgs.UserCreated created)
                    {
                        Assert.Equal(_id, created.Id);
                        Assert.Equal(AuthProvider, created.AuthProvider);
                        Assert.Equal(AuthDomain, created.AuthDomain);
                        Assert.Equal(UserName, created.UserName);
                        Assert.Equal(emptyGivenName, created.GivenName);
                        Assert.Equal(Surname, created.Surname);
                        Assert.Equal(Email, created.Email);
                    }
                    else
                    {
                        throw new Exception("wrong event.");
                    }
                });
        }

        [Fact]
        public void can_create_new_user_with_empty_surname()
        {
            string emptySurname = string.Empty;
            var user = new IdentityAgg(
                _id,
                _userSidFromAuthProvider,
                AuthProvider,
                AuthDomain,
                UserName,
                FullName,
                GivenName,
                emptySurname,
                Email,
                _command);
            var events = user.TakeEvents();
            Assert.Collection(
                events,
                e =>
                {
                    if (e is PolicyUserMsgs.UserCreated created)
                    {
                        Assert.Equal(_id, created.Id);
                        Assert.Equal(AuthProvider, created.AuthProvider);
                        Assert.Equal(AuthDomain, created.AuthDomain);
                        Assert.Equal(UserName, created.UserName);
                        Assert.Equal(GivenName, created.GivenName);
                        Assert.Equal(emptySurname, created.Surname);
                        Assert.Equal(Email, created.Email);
                    }
                    else
                    {
                        throw new Exception("wrong event.");
                    }
                });
        }
        [Fact]
        public void cannot_create_user_with_empty_provider_name()
        {
            Assert.Throws<ArgumentNullException>(
                () => new IdentityAgg(
                            _id,
                            _userSidFromAuthProvider,
                            string.Empty,
                            AuthDomain,
                            UserName,
                            FullName,
                            GivenName,
                            Surname,
                            Email,
                            _command));
        }

        [Fact]
        public void cannot_create_user_with_empty_domain_name()
        {
            Assert.Throws<ArgumentNullException>(
                () => new IdentityAgg(
                            _id,
                            _userSidFromAuthProvider,
                            AuthProvider,
                            string.Empty,
                            UserName,
                            FullName,
                            GivenName,
                            Surname,
                            Email,
                            _command));
        }

        [Fact]
        public void cannot_create_user_with_empty_user_name()
        {
            Assert.Throws<ArgumentNullException>(
                () => new IdentityAgg(
                            _id,
                            _userSidFromAuthProvider,
                            AuthProvider,
                            AuthDomain,
                            string.Empty,
                            FullName,
                            GivenName,
                            Surname,
                            Email,
                            _command));
        }
        
        [Fact]
        public void cannot_create_user_with_malformed_email()
        {
            Assert.Throws<FormatException>(
                () => new IdentityAgg(
                            _id,
                            _userSidFromAuthProvider,
                            AuthProvider,
                            AuthDomain,
                            UserName,
                            FullName,
                            GivenName,
                            Surname,
                            "joe",
                            _command));
        }

        [Fact]
        public void can_log_authentication()
        {
            throw new NotImplementedException();
            /*
            var user = new IdentityAgg(
                            _id,
                            _userSidFromAuthProvider,
                            AuthProvider,
                            AuthDomain,
                            UserName,
                            FullName,
                            GivenName,
                            Surname,
                            Email,
                            _command);
            user.Authenticated(HostIPAddress);
            var events = user.TakeEvents();
            Assert.Collection(
                            events,
                            e =>
                            {
                                if (e is UserMsgs.UserCreated created)
                                {
                                    Assert.Equal(_id, created.Id);
                                }
                                else
                                {
                                    throw new Exception("wrong event.");
                                }
                            },
                            e =>
                            {
                                if (e is UserMsgs.Authenticated authenticated)
                                {
                                    Assert.Equal(_id, authenticated.Id);
                                }
                                else
                                {
                                    throw new Exception("wrong event.");
                                }
                            });
            */
        }

        [Fact]
        public void can_updateGivenName()
        {
            var user = new IdentityAgg(
                            _id,
                            _userSidFromAuthProvider,
                            AuthProvider,
                            AuthDomain,
                            UserName,
                            FullName,
                            GivenName,
                            Surname,
                            Email,
                            _command);
            user.UpdateGivenName(GivenNameUpdate);
            var events = user.TakeEvents();
            Assert.Collection(
                            events,
                            e =>
                            {
                                if (e is PolicyUserMsgs.UserCreated created)
                                {
                                    Assert.Equal(_id, created.Id);
                                }
                                else
                                {
                                    throw new Exception("wrong event.");
                                }
                            },
                            e =>
                            {
                                if (e is PolicyUserMsgs.GivenNameUpdated giveNameUpdated)
                                {
                                    Assert.Equal(_id, giveNameUpdated.UserId);
                                    Assert.Equal(GivenNameUpdate, giveNameUpdated.GivenName);
                                }
                                else
                                {
                                    throw new Exception("wrong event.");
                                }
                            });
        }
        [Fact]
        public void cannot_updateGivenName_with_empty_givename()
        {
            var user = new IdentityAgg(
                            _id,
                            _userSidFromAuthProvider,
                            AuthProvider,
                            AuthDomain,
                            UserName,
                            FullName,
                            GivenName,
                            Surname,
                            Email,
                            _command);
            Assert.Throws<ArgumentNullException>(
                () => user.UpdateGivenName(string.Empty));

        }
        [Fact]
        public void can_updateSurName()
        {
            var user = new IdentityAgg(
                            _id,
                            _userSidFromAuthProvider,
                            AuthProvider,
                            AuthDomain,
                            UserName,
                            FullName,
                            GivenName,
                            Surname,
                            Email,
                            _command);
            user.UpdateSurname(SurnameUpdate);
            var events = user.TakeEvents();
            Assert.Collection(
                            events,
                            e =>
                            {
                                if (e is PolicyUserMsgs.UserCreated created)
                                {
                                    Assert.Equal(_id, created.Id);
                                }
                                else
                                {
                                    throw new Exception("wrong event.");
                                }
                            },
                            e =>
                            {
                                if (e is PolicyUserMsgs.SurnameUpdated surnameUpdated)
                                {
                                    Assert.Equal(_id, surnameUpdated.UserId);
                                    Assert.Equal(SurnameUpdate, surnameUpdated.Surname);
                                }
                                else
                                {
                                    throw new Exception("wrong event.");
                                }
                            });
        }
        [Fact]
        public void cannot_updateSurName_with_empty_surname()
        {
            var user = new IdentityAgg(
                            _id,
                            _userSidFromAuthProvider,
                            AuthProvider,
                            AuthDomain,
                            UserName,
                            FullName,
                            GivenName,
                            Surname,
                            Email,
                            _command);
            Assert.Throws<ArgumentNullException>(
                () => user.UpdateSurname(string.Empty));

        }
        [Fact]
        public void can_updateFullName()
        {
            var user = new IdentityAgg(
                            _id,
                            _userSidFromAuthProvider,
                            AuthProvider,
                            AuthDomain,
                            UserName,
                            FullName,
                            GivenName,
                            Surname,
                            Email,
                            _command);
            user.UpdateFullName(FullNameUpdate);
            var events = user.TakeEvents();
            Assert.Collection(
                            events,
                            e =>
                            {
                                if (e is PolicyUserMsgs.UserCreated created)
                                {
                                    Assert.Equal(_id, created.Id);
                                }
                                else
                                {
                                    throw new Exception("wrong event.");
                                }
                            },
                            e =>
                            {
                                if (e is PolicyUserMsgs.FullNameUpdated fullNameUpdated)
                                {
                                    Assert.Equal(_id, fullNameUpdated.UserId);
                                    Assert.Equal(FullNameUpdate, fullNameUpdated.FullName);
                                }
                                else
                                {
                                    throw new Exception("wrong event.");
                                }
                            });
        }
        [Fact]
        public void cannot_updateFullName_with_empty_fullname()
        {
            var user = new IdentityAgg(
                            _id,
                            _userSidFromAuthProvider,
                            AuthProvider,
                            AuthDomain,
                            UserName,
                            FullName,
                            GivenName,
                            Surname,
                            Email,
                            _command);
            Assert.Throws<ArgumentNullException>(
                () => user.UpdateFullName(string.Empty));

        }
        [Fact]
        public void can_updateEmail()
        {
            var user = new IdentityAgg(
                            _id,
                            _userSidFromAuthProvider,
                            AuthProvider,
                            AuthDomain,
                            UserName,
                            FullName,
                            GivenName,
                            Surname,
                            Email,
                            _command);
            user.UpdateEmail(EmailUpdate);
            var events = user.TakeEvents();
            Assert.Collection(
                            events,
                            e =>
                            {
                                if (e is PolicyUserMsgs.UserCreated created)
                                {
                                    Assert.Equal(_id, created.Id);
                                }
                                else
                                {
                                    throw new Exception("wrong event.");
                                }
                            },
                            e =>
                            {
                                if (e is PolicyUserMsgs.EmailUpdated emailUpdated)
                                {
                                    Assert.Equal(_id, emailUpdated.UserId);
                                    Assert.Equal(EmailUpdate, emailUpdated.Email);
                                }
                                else
                                {
                                    throw new Exception("wrong event.");
                                }
                            });
        }
        [Fact]
        public void cannot_updateEmail_with_empty_email()
        {
            var user = new IdentityAgg(
                            _id,
                            _userSidFromAuthProvider,
                            AuthProvider,
                            AuthDomain,
                            UserName,
                            FullName,
                            GivenName,
                            Surname,
                            Email,
                            _command);
            Assert.Throws<ArgumentNullException>(
                () => user.UpdateEmail(string.Empty));

        }
        [Fact]
        public void cannot_updateEmail_with_malformed_email()
        {
            var user = new IdentityAgg(
                            _id,
                            _userSidFromAuthProvider,
                            AuthProvider,
                            AuthDomain,
                            UserName,
                            FullName,
                            GivenName,
                            Surname,
                            Email,
                            _command);
            Assert.Throws<FormatException>(
                () => user.UpdateEmail("Joe"));
        }
       
        [Fact]
        public void can_updateAuthDomain()
        {
            var user = new IdentityAgg(
                            _id,
                            _userSidFromAuthProvider,
                            AuthProvider,
                            AuthDomain,
                            UserName,
                            FullName,
                            GivenName,
                            Surname,
                            Email,
                            _command);
            user.UpdateAuthDomain(AuthDomainUpdate);
            var events = user.TakeEvents();
            Assert.Collection(
                            events,
                            e =>
                            {
                                if (e is PolicyUserMsgs.UserCreated created)
                                {
                                    Assert.Equal(_id, created.Id);
                                }
                                else
                                {
                                    throw new Exception("wrong event.");
                                }
                            },
                            e =>
                            {
                                if (e is PolicyUserMsgs.AuthDomainUpdated authDomainUpdate)
                                {
                                    Assert.Equal(_id, authDomainUpdate.UserId);
                                    Assert.Equal(AuthDomainUpdate, authDomainUpdate.AuthDomain);
                                }
                                else
                                {
                                    throw new Exception("wrong event.");
                                }
                            });
        }
        [Fact]
        public void cannot_updateAuthDomain_with_empty_authdomain()
        {
            var user = new IdentityAgg(
                            _id,
                            _userSidFromAuthProvider,
                            AuthProvider,
                            AuthDomain,
                            UserName,
                            FullName,
                            GivenName,
                            Surname,
                            Email,
                            _command);
            Assert.Throws<ArgumentNullException>(
                () => user.UpdateAuthDomain(string.Empty));

        }
        [Fact]
        public void can_update_username()
        {
            var user = new IdentityAgg(
                            _id,
                            _userSidFromAuthProvider,
                            AuthProvider,
                            AuthDomain,
                            UserName,
                            FullName,
                            GivenName,
                            Surname,
                            Email,
                            _command);
            user.UpdateUserName(UserNameUpdate);
            var events = user.TakeEvents();
            Assert.Collection(
                            events,
                            e =>
                            {
                                if (e is PolicyUserMsgs.UserCreated created)
                                {
                                    Assert.Equal(_id, created.Id);
                                }
                                else
                                {
                                    throw new Exception("wrong event.");
                                }
                            },
                            e =>
                            {
                                if (e is PolicyUserMsgs.UserNameUpdated userNameUpdate)
                                {
                                    Assert.Equal(_id, userNameUpdate.UserId);
                                    Assert.Equal(UserNameUpdate, userNameUpdate.UserName);
                                }
                                else
                                {
                                    throw new Exception("wrong event.");
                                }
                            });
        }
        [Fact]
        public void cannot_update_username_with_empty_username()
        {
            var user = new IdentityAgg(
                            _id,
                            _userSidFromAuthProvider,
                            AuthProvider,
                            AuthDomain,
                            UserName,
                            FullName,
                            GivenName,
                            Surname,
                            Email,
                            _command);
            Assert.Throws<ArgumentNullException>(
                () => user.UpdateUserName(string.Empty));

        }        
    }
}
