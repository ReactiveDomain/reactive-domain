using System;
using System.Linq;
using ReactiveDomain.Messaging;
using ReactiveDomain.Users.Policy;
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
                r => r.WithChildRole("user",
                     cr => cr.WithCommand<DoStuff>(default),
                     cr => cr.WithCommand<DoOtherStuff>(default)),
                    
                r => r.WithChildRole("viewer",
                     cr => cr.WithCommand<SeeStuff>(default),
                     cr => cr.WithCommand<SeeOtherStuff>(default)),
            
                r => r.WithCommand<Admin>(default));
            var policy = builder.Build();

            //application
            Assert.Equal("foo", policy.OwningApplication.Name);
            Assert.Equal(version.ToString(), policy.OwningApplication.Version);

            //all empty ids
            Assert.Equal(Guid.Empty, policy.OwningApplication.Id);
            Assert.True(policy.Roles.All(r => r.RoleId == Guid.Empty));
            
            Assert.Collection(policy.Permissions,
                p => Assert.True(p.Matches(typeof(DoStuff))),
                p => Assert.True(p.Matches(typeof(DoOtherStuff))),
                p => Assert.True(p.Matches(typeof(SeeStuff))),
                p => Assert.True(p.Matches(typeof(SeeOtherStuff))),
                p => Assert.True(p.Matches(typeof(Admin)))
            );
            
            Assert.Collection(policy.Permissions,
                p => Assert.True(p.Matches<DoStuff>()),
                p => Assert.True(p.Matches<DoOtherStuff>()),
                p => Assert.True(p.Matches<SeeStuff>()),
                p => Assert.True(p.Matches<SeeOtherStuff>()),
                p => Assert.True(p.Matches<Admin>())
            );

            Assert.Collection(policy.Roles,
                r => Assert.Equal("admin", r.Name),
                r => Assert.Equal("user", r.Name),
                r => Assert.Equal("viewer", r.Name));

            //admin role
            Assert.Collection(policy.Roles[0].Permissions,
                p => Assert.True(p.Matches(typeof(DoStuff))),
                p => Assert.True(p.Matches(typeof(DoOtherStuff))),
                p => Assert.True(p.Matches(typeof(SeeStuff))),
                p => Assert.True(p.Matches(typeof(SeeOtherStuff))),
                p => Assert.True(p.Matches(typeof(Admin)))
            );

            //admin role
            Assert.Collection(policy.Roles[0].Permissions,
                p => Assert.True(p.Matches<DoStuff>()),
                p => Assert.True(p.Matches<DoOtherStuff>()),
                p => Assert.True(p.Matches<SeeStuff>()),
                p => Assert.True(p.Matches<SeeOtherStuff>()),
                p => Assert.True(p.Matches<Admin>())
            );

            Assert.Collection(policy.Roles[0].ChildRoles,
                    cr => Assert.Equal("user", cr.Name),
                    cr => Assert.Equal("viewer", cr.Name));
            //user
            Assert.Collection(policy.Roles[1].Permissions,
                p => Assert.True(p.Matches<DoStuff>()),
                p => Assert.True(p.Matches<DoOtherStuff>()));
            
            Assert.Collection(policy.Roles[1].Permissions,
                p => Assert.True(p.Matches(typeof(DoStuff))),
                p => Assert.True(p.Matches(typeof(DoOtherStuff))));
            
            Assert.Empty(policy.Roles[1].ChildRoles);
            
            //viewer
            Assert.Collection(policy.Roles[2].Permissions,
                p => Assert.True(p.Matches<SeeStuff>()),
                p => Assert.True(p.Matches<SeeOtherStuff>()));
            Assert.Collection(policy.Roles[2].Permissions,
                p => Assert.True(p.Matches(typeof(SeeStuff))),
                p => Assert.True(p.Matches(typeof(SeeOtherStuff))));
            Assert.Empty(policy.Roles[2].ChildRoles);

        }

        [Fact]
        public void A_command_can_be_matched()
        {
            var p = new Permission(typeof(DoStuff));
            
            Assert.True(p.Matches(typeof(DoStuff)));
        }
        
        class DoStuff : Command { }
        class DoOtherStuff : Command { }
        class SeeStuff : Command { }
        class SeeOtherStuff : Command { }
        class Admin : Command { }
    }
}
