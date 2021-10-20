using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace ReactiveDomain.Identity.Tests
{
    public class SubjectAggTests
    {
        /*
        [Fact]
        public void can_log_authentication()
        {
            throw new NotImplementedException();

            var user = new User(
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

        }
        */
    }
}
