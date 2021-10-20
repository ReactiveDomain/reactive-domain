using System;
using ReactiveDomain.Messaging;
using ReactiveDomain.Users.Domain;
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
            var user = new User(
                            _id,
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
                                if (e is UserMsgs.UserCreated created)
                                {
                                    Assert.Equal(_id, created.UserId);
                                }
                                else
                                {
                                    throw new Exception("wrong event.");
                                }
                            },
                            e =>
                            {
                                if (e is UserMsgs.UserDetailsUpdated details)
                                {
                                    Assert.Equal(_id, details.UserId);
                                    Assert.Equal(FullName, details.FullName);
                                    Assert.Equal(GivenName, details.GivenName);
                                    Assert.Equal(Surname, details.Surname);
                                    Assert.Equal(Email, details.Email);
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
            var user = new User(
                _id,
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
                    if (e is UserMsgs.UserCreated created)
                    {
                        Assert.Equal(_id, created.UserId);
                    }
                    else
                    {
                        throw new Exception("wrong event.");
                    }
                },
                e =>
                {
                    if (e is UserMsgs.UserDetailsUpdated details)
                    {
                        Assert.Equal(_id, details.UserId);
                        Assert.Equal(FullName, details.FullName);
                        Assert.Equal(GivenName, details.GivenName);
                        Assert.Equal(Surname, details.Surname);
                        Assert.Equal(emptyEmail, details.Email);
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
            var user = new User(
                _id,
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
                    if (e is UserMsgs.UserCreated created)
                    {
                        Assert.Equal(_id, created.UserId);
                    }
                    else
                    {
                        throw new Exception("wrong event.");
                    }
                },
                e =>
                {
                    if (e is UserMsgs.UserDetailsUpdated details)
                    {
                        Assert.Equal(_id, details.UserId);
                        Assert.Equal(FullName, details.FullName);
                        Assert.Equal(emptyGivenName, details.GivenName);
                        Assert.Equal(Surname, details.Surname);
                        Assert.Equal(Email, details.Email);
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
            var user = new User(
                _id,
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
                     if (e is UserMsgs.UserCreated created)
                     {
                         Assert.Equal(_id, created.UserId);
                     }
                     else
                     {
                         throw new Exception("wrong event.");
                     }
                 },
                e =>
                {
                    if (e is UserMsgs.UserDetailsUpdated details)
                    {
                        Assert.Equal(_id, details.UserId);
                        Assert.Equal(FullName, details.FullName);
                        Assert.Equal(GivenName, details.GivenName);
                        Assert.Equal(emptySurname, details.Surname);
                        Assert.Equal(Email, details.Email);
                    }
                    else
                    {
                        throw new Exception("wrong event.");
                    }
                });
        }

        [Fact]
        public void cannot_create_user_with_empty_ids()
        {
            Assert.Throws<ArgumentException>(
                () => new User(
                            Guid.Empty,
                            FullName,
                            GivenName,
                            Surname,
                            Email,
                            _command));
            Assert.Throws<ArgumentNullException>(
               () => new User(
                           _id,
                           FullName,
                           GivenName,
                           Surname,
                           Email,
                           null));

        }
        [Fact]
        public void cannot_create_user_with_malformed_email()
        {
            Assert.Throws<FormatException>(
                () => new User(
                            _id,
                            FullName,
                            GivenName,
                            Surname,
                            "joe",
                            _command));
        }
        [Fact]
        public void can_mapp_authdomain()
        {
            var user = new User(
                            _id,
                            FullName,
                            GivenName,
                            Surname,
                            Email,
                            _command);
            user.MapToAuthDomain(
              _userSidFromAuthProvider,
              AuthProvider,
              AuthDomain,
              UserName);
            var events = user.TakeEvents();
            Assert.Collection(
                            events,
                            e =>
                            {
                                if (e is UserMsgs.UserCreated created)
                                {
                                    Assert.Equal(_id, created.UserId);
                                }
                                else
                                {
                                    throw new Exception("wrong event.");
                                }
                            },
                            e =>
                            {
                                if (e is UserMsgs.UserDetailsUpdated details)
                                {
                                    Assert.Equal(_id, details.UserId);
                                    Assert.Equal(FullName, details.FullName);
                                    Assert.Equal(GivenName, details.GivenName);
                                    Assert.Equal(Surname, details.Surname);
                                    Assert.Equal(Email, details.Email);
                                }
                                else
                                {
                                    throw new Exception("wrong event.");
                                }
                            },
                            e =>
                            {
                                if (e is UserMsgs.AuthDomainMapped mapped)
                                {
                                    Assert.Equal(_id, mapped.UserId);
                                    Assert.Equal(_userSidFromAuthProvider, mapped.SubjectId);
                                    Assert.Equal(AuthProvider, mapped.AuthProvider);
                                    Assert.Equal(AuthDomain, mapped.AuthDomain);
                                    Assert.Equal(UserName, mapped.UserName);
                                }
                                else
                                {
                                    throw new Exception("wrong event.");
                                }
                            });
        }

        [Fact]
        public void can_map_additional_authdomains()
        {
            var otherSid = "other SID";
            var user = new User(
                            _id,
                            FullName,
                            GivenName,
                            Surname,
                            Email,
                            _command);
            user.MapToAuthDomain(
             _userSidFromAuthProvider,
             AuthProvider,
             AuthDomain,
             UserName);
            user.MapToAuthDomain(
             otherSid,
             AuthProvider,
             AuthDomain,
             UserName);
            var events = user.TakeEvents();
            Assert.Collection(
                            events,
                            e =>
                            {
                                if (e is UserMsgs.UserCreated created)
                                {
                                    Assert.Equal(_id, created.UserId);
                                }
                                else
                                {
                                    throw new Exception("wrong event.");
                                }
                            },
                            e =>
                            {
                                if (e is UserMsgs.UserDetailsUpdated details)
                                {
                                    Assert.Equal(_id, details.UserId);
                                    Assert.Equal(FullName, details.FullName);
                                    Assert.Equal(GivenName, details.GivenName);
                                    Assert.Equal(Surname, details.Surname);
                                    Assert.Equal(Email, details.Email);
                                }
                                else
                                {
                                    throw new Exception("wrong event.");
                                }
                            },
                            e =>
                            {
                                if (e is UserMsgs.AuthDomainMapped mapped)
                                {
                                    Assert.Equal(_id, mapped.UserId);
                                    Assert.Equal(_userSidFromAuthProvider, mapped.SubjectId);
                                    Assert.Equal(AuthProvider, mapped.AuthProvider);
                                    Assert.Equal(AuthDomain, mapped.AuthDomain);
                                    Assert.Equal(UserName, mapped.UserName);
                                }
                                else
                                {
                                    throw new Exception("wrong event.");
                                }
                            },
                             e =>
                             {
                                 if (e is UserMsgs.AuthDomainMapped mapped)
                                 {
                                     Assert.Equal(_id, mapped.UserId);
                                     Assert.Equal(otherSid, mapped.SubjectId);
                                     Assert.Equal(AuthProvider, mapped.AuthProvider);
                                     Assert.Equal(AuthDomain, mapped.AuthDomain);
                                     Assert.Equal(UserName, mapped.UserName);
                                 }
                                 else
                                 {
                                     throw new Exception("wrong event.");
                                 }
                             });

        }
        [Fact]
        public void cannot_remap_same_authdomain()
        {
            var user = new User(
                           _id,
                           FullName,
                           GivenName,
                           Surname,
                           Email,
                           _command);
            user.MapToAuthDomain(
             _userSidFromAuthProvider,
             AuthProvider,
             AuthDomain,
             UserName);

            Assert.Throws<ArgumentException>(
                  () => user.MapToAuthDomain(
                            _userSidFromAuthProvider,
                            AuthProvider,
                            AuthDomain,
                            UserName));

        }






        [Fact]
        public void can_update_name_details()
        {
            var newFullName = "newFullName";
            var newGivenName = "newGivenName";
            var newSurname = "newSurname";
            var newEmail = "joe@bob.com";

            var user = new User(
                            _id,
                            FullName,
                            GivenName,
                            Surname,
                            Email,
                            _command);
            user.UpdateNameDetails(newGivenName, newSurname, newFullName, newEmail);
            var events = user.TakeEvents();
            Assert.Collection(
                          events,
                          e =>
                          {
                              if (e is UserMsgs.UserCreated created)
                              {
                                  Assert.Equal(_id, created.UserId);
                              }
                              else
                              {
                                  throw new Exception("wrong event.");
                              }
                          },
                          e =>
                          {
                              if (e is UserMsgs.UserDetailsUpdated details)
                              {
                                  Assert.Equal(_id, details.UserId);
                                  Assert.Equal(FullName, details.FullName);
                                  Assert.Equal(GivenName, details.GivenName);
                                  Assert.Equal(Surname, details.Surname);
                                  Assert.Equal(Email, details.Email);
                              }
                              else
                              {
                                  throw new Exception("wrong event.");
                              }
                          },
                          e =>
                          {
                              if (e is UserMsgs.UserDetailsUpdated details)
                              {
                                  Assert.Equal(_id, details.UserId);
                                  Assert.Equal(newFullName, details.FullName);
                                  Assert.Equal(newGivenName, details.GivenName);
                                  Assert.Equal(newSurname, details.Surname);
                                  Assert.Equal(newEmail, details.Email);
                              }
                              else
                              {
                                  throw new Exception("wrong event.");
                              }
                          });
        }

        [Fact]
        public void cannot_update_nameDetails_with_empty_strings()
        {
            var user = new User(
                        _id,
                        FullName,
                        GivenName,
                        Surname,
                        Email,
                        _command);
            Assert.Throws<ArgumentNullException>(
                () => user.UpdateNameDetails(fullName: string.Empty));
            Assert.Throws<ArgumentNullException>(
              () => user.UpdateNameDetails(givenName: string.Empty));
            Assert.Throws<ArgumentNullException>(
              () => user.UpdateNameDetails(surName: string.Empty));
            Assert.Throws<ArgumentNullException>(
              () => user.UpdateNameDetails(email: string.Empty));

        }

        [Fact]
        public void cannot_updateEmail_with_malformed_email()
        {
            var user = new User(
                            _id,
                            FullName,
                            GivenName,
                            Surname,
                            Email,
                            _command);
            Assert.Throws<FormatException>(
                () => user.UpdateNameDetails(email: "Joe"));
        }



    }
}
