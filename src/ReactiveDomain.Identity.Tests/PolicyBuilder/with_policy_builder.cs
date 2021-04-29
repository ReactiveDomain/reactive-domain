using System;
using System.Linq;
using ReactiveDomain.Messaging;
using ReactiveDomain.Users.PolicyModel;
using Xunit;

namespace ReactiveDomain.Users.Tests.PolicyBuilder
{
    // ReSharper disable once InconsistentNaming
    public class with_policy_builder
    {
        [Fact]
        public void can_build_policy()
        {
            var version = new Version(1, 1);
            var builder = new SecurityPolicyBuilder("foo", version);
            
            builder.AddRole("admin",
                r => r.WithCommand(typeof(DoStuff)),
                r => r.WithCommand(typeof(DoOtherStuff)),
                r => r.WithCommand(typeof(SeeStuff)),
                r => r.WithCommand(typeof(SeeOtherStuff)),
                r => r.WithCommand(typeof(Admin)));

            builder.AddRole("user",
                r => r.WithCommand(typeof(DoStuff)),
                r => r.WithCommand(typeof(DoOtherStuff)),
                r => r.WithCommand(typeof(SeeStuff)),
                r => r.WithCommand(typeof(SeeOtherStuff)),
                r => r.WithCommand(typeof(Admin)));
            
            builder.AddRole("viewer",
                r => r.WithCommand(typeof(SeeStuff)),
                r => r.WithCommand(typeof(SeeOtherStuff)));
            
            var policy = builder.Build();

            //application
            Assert.Equal("foo", policy.OwningApplication.Name);
            Assert.Equal(version.ToString(), policy.OwningApplication.Version);

            //all empty ids
            Assert.Equal(Guid.Empty, policy.OwningApplication.Id);
            Assert.True(policy.Roles.All(r => r.RoleId == Guid.Empty));

            Assert.Collection(policy.Roles,
                r => Assert.Equal("admin", r.Name),
                r => Assert.Equal("user", r.Name),
                r => Assert.Equal("viewer", r.Name));

            //admin role
            Assert.Collection(policy.Roles[0].AllowedPermissions,
                p => Assert.True(p == typeof(DoStuff)),
                p => Assert.True(p == typeof(DoOtherStuff)),
                p => Assert.True(p == typeof(SeeStuff)),
                p => Assert.True(p == typeof(SeeOtherStuff)),
                p => Assert.True(p == typeof(Admin))
            );
        }
        
        class DoStuff : Command { }
        class DoOtherStuff : Command { }
        class SeeStuff : Command { }
        class SeeOtherStuff : Command { }
        class Admin : Command { }
    }
}
