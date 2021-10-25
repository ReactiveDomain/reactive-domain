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
        private const string AuthDomain = "CompanyNet";
        private const string UserName = "jsmith";
        private const string GivenName = "John";
        private const string Surname = "Smith";
        private const string FullName = "John Smith";
        private const string Email = "john.smith@Company1.com";
        private const string HostIPAddress = "127.0.0.1";
        private const string GivenNameUpdate = "John Update";
        private const string SurnameUpdate = "Smith Update";
        private const string FullNameUpdate = "John Smith Update";
        private const string EmailUpdate = "john.smithUpdate@Company1.com";
        private readonly string UserSidFromAuthProviderUpdate = Guid.NewGuid().ToString() + "Update";
        private readonly string AuthDomainUpdate = "CompanyNet Update";
        private readonly string UserNameUpdate = "jsmith Update";
        private const string ClientScope = "APPLICATION1";
        private const string ClientScope2 = "APPLICATION2";


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
        [Fact]
        public void can_add_client_scope()
        {
            var user = new User(
                              _id,
                              FullName,
                              GivenName,
                              Surname,
                              Email,
                              _command);
            user.AddClientScope(ClientScope); //add scope
            user.AddClientScope(ClientScope2); //add second scope
            user.AddClientScope(ClientScope); //add duplicate scope n.b. should be idempotent

            var events = user.TakeEvents();
            Assert.Collection(
                            events,
                            e =>
                            {
                                var created = Assert.IsType<UserMsgs.UserCreated>(e);
                                Assert.Equal(_id, created.UserId);
                            },
                            e => Assert.IsType<UserMsgs.UserDetailsUpdated>(e),
                            e =>
                            {
                                var added = Assert.IsType<UserMsgs.ClientScopeAdded>(e);
                                Assert.Equal(_id, added.UserId);
                                Assert.Equal(ClientScope, added.ClientScope);
                            },
                            e =>
                            {
                                var added = Assert.IsType<UserMsgs.ClientScopeAdded>(e);
                                Assert.Equal(_id, added.UserId);
                                Assert.Equal(ClientScope2, added.ClientScope);
                            }
                            // idempotent add produces no second added event
                           );
        }
        [Fact]
        public void can_remove_client_scope()
        {
            var user = new User(
                                 _id,
                                 FullName,
                                 GivenName,
                                 Surname,
                                 Email,
                                 _command);
            user.RemoveClientScope(ClientScope2); //no scopes should be idempotent
            user.AddClientScope(ClientScope); //add scope
            user.RemoveClientScope(ClientScope); //remove scope
            user.RemoveClientScope(ClientScope); //second removal should be idempotent
            user.AddClientScope(ClientScope); //can re-add
            user.RemoveClientScope(ClientScope); //can remove re-added

            var events = user.TakeEvents();
            Assert.Collection(
                            events,
                            e =>
                            {
                                var created = Assert.IsType<UserMsgs.UserCreated>(e);
                                Assert.Equal(_id, created.UserId);
                            },
                            e => Assert.IsType<UserMsgs.UserDetailsUpdated>(e),
                            // no scopes should be idempotent
                            e => //add
                            {
                                var added = Assert.IsType<UserMsgs.ClientScopeAdded>(e);
                                Assert.Equal(_id, added.UserId);
                                Assert.Equal(ClientScope, added.ClientScope);
                            },
                            e => //remove
                            {
                                var removed = Assert.IsType<UserMsgs.ClientScopeRemoved>(e);
                                Assert.Equal(_id, removed.UserId);
                                Assert.Equal(ClientScope, removed.ClientScope);
                            },
                             // idempotent remove produces no second event
                             e => //re-add
                             {
                                 var added = Assert.IsType<UserMsgs.ClientScopeAdded>(e);
                                 Assert.Equal(_id, added.UserId);
                                 Assert.Equal(ClientScope, added.ClientScope);
                             },
                            e => //removed re-added
                            {
                                var removed = Assert.IsType<UserMsgs.ClientScopeRemoved>(e);
                                Assert.Equal(_id, removed.UserId);
                                Assert.Equal(ClientScope, removed.ClientScope);
                            }
                           );
        }
        [Fact]
        public void cannot_add_empty_client_scope()
        {
            var user = new User(
                                _id,
                                FullName,
                                GivenName,
                                Surname,
                                Email,
                                _command);
            Assert.Throws<ArgumentOutOfRangeException>(() => user.AddClientScope(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => user.AddClientScope(string.Empty));
            Assert.Throws<ArgumentOutOfRangeException>(() => user.AddClientScope("\t"));
        }
        [Fact]
        public void cannot_remove_empty_client_scope()
        {
            var user = new User(
                               _id,
                               FullName,
                               GivenName,
                               Surname,
                               Email,
                               _command);
            Assert.Throws<ArgumentOutOfRangeException>(() => user.RemoveClientScope(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => user.RemoveClientScope(string.Empty));
            Assert.Throws<ArgumentOutOfRangeException>(() => user.RemoveClientScope("\t"));
        }

        [Fact]
        public void can_deactivate_user()
        {
            var user = new User(
                              _id,
                              FullName,
                              GivenName,
                              Surname,
                              Email,
                              _command);
            user.Deactivate();
            user.Deactivate();  //idempotent deactivation

            var events = user.TakeEvents();
            Assert.Collection(
                            events,
                            e =>
                            {
                                var created = Assert.IsType<UserMsgs.UserCreated>(e);
                                Assert.Equal(_id, created.UserId);
                            },
                            e => Assert.IsType<UserMsgs.UserDetailsUpdated>(e),
                            e =>
                            {
                                var deactivated = Assert.IsType<UserMsgs.Deactivated>(e);
                                Assert.Equal(_id, deactivated.UserId);
                            }
                            //idempotent deactivation
                           );
        }
        [Fact]
        public void can_reactivate_user()
        {
            var user = new User(
                             _id,
                             FullName,
                             GivenName,
                             Surname,
                             Email,
                             _command);
            user.Deactivate();
            user.Reactivate();
            user.Reactivate(); //idempotent
            user.Deactivate();

            var events = user.TakeEvents();
            Assert.Collection(
                            events,
                            e =>
                            {
                                var created = Assert.IsType<UserMsgs.UserCreated>(e);
                                Assert.Equal(_id, created.UserId);
                            },
                            e => Assert.IsType<UserMsgs.UserDetailsUpdated>(e),
                            e =>
                            {
                                var deactivated = Assert.IsType<UserMsgs.Deactivated>(e);
                                Assert.Equal(_id, deactivated.UserId);
                            },
                            e =>
                            {
                                var reactivated = Assert.IsType<UserMsgs.Activated>(e);
                                Assert.Equal(_id, reactivated.UserId);
                            },
                            //idempotent reactivation
                            e =>
                            {
                                var deactivated = Assert.IsType<UserMsgs.Deactivated>(e);
                                Assert.Equal(_id, deactivated.UserId);
                            }                           
                           );
        }
    }
}
