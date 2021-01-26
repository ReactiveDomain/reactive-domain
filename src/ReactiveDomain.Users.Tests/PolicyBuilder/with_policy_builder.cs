using System;
using System.Linq;
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
                     cr => cr.WithPermission("do-stuff"),
                                    cr => cr.WithPermission("do-other-stuff")),
                                r => r.WithChildRole("viewer",
                     cr => cr.WithPermission("see-stuff"),
                                    cr => cr.WithPermission("see-other-stuff")),
                                r => r.WithPermission("admin"));
            var policy = builder.Build();

            //application
            Assert.Equal("foo", policy.OwningApplication.Name);
            Assert.Equal(version.ToString(), policy.OwningApplication.Version);

            //all empty ids
            Assert.Equal(Guid.Empty, policy.OwningApplication.Id);
            Assert.True(policy.Permissions.All(p => p.Id == Guid.Empty));
            Assert.True(policy.Roles.All(r => r.RoleId == Guid.Empty));

            Assert.Collection(policy.Permissions,
                p => Assert.Equal("do-stuff", p.Name),
                p => Assert.Equal("do-other-stuff", p.Name),
                p => Assert.Equal("see-stuff", p.Name),
                p => Assert.Equal("see-other-stuff", p.Name),
                p => Assert.Equal("admin", p.Name));

            Assert.Collection(policy.Roles,
                r => Assert.Equal("admin", r.Name),
                r => Assert.Equal("user", r.Name),
                r => Assert.Equal("viewer", r.Name));
            //admin role
            Assert.Collection(policy.Roles[0].Permissions,
                p => Assert.Equal("do-stuff", p.Name),
                p => Assert.Equal("do-other-stuff", p.Name),
                p => Assert.Equal("see-stuff", p.Name),
                p => Assert.Equal("see-other-stuff", p.Name),
                p => Assert.Equal("admin", p.Name));

            Assert.Collection(policy.Roles[0].ChildRoles,
                    cr => Assert.Equal("user", cr.Name),
                    cr => Assert.Equal("viewer", cr.Name));
            //user
            Assert.Collection(policy.Roles[1].Permissions,
                p => Assert.Equal("do-stuff", p.Name),
                p => Assert.Equal("do-other-stuff", p.Name));
            Assert.Empty(policy.Roles[1].ChildRoles);
            //viewer
            Assert.Collection(policy.Roles[2].Permissions,
                p => Assert.Equal("see-stuff", p.Name),
                p => Assert.Equal("see-other-stuff", p.Name));
            Assert.Empty(policy.Roles[2].ChildRoles);

        }
    }
}
