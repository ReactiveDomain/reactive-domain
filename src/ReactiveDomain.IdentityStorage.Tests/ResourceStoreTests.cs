using IdentityServer4.Models;
using ReactiveDomain.IdentityStorage.Services;
using Xunit;

namespace ReactiveDomain.Users.Services
{
    public class ResourceStoreTests
    {
        private readonly Resources resources = new Resources();
        private readonly ResourcesStore store = new ResourcesStore();
        private readonly IdentityResource resource1;
        private readonly IdentityResource resource2;
        private readonly string name1 = "openid";
        private readonly string name2 = "rd-policy";
        private readonly string name3 = "other";


        public ResourceStoreTests()
        {

            resource1 = new IdentityResource(
                name: name1,
                userClaims: new[] { "sub" },
                displayName: "Your user identifier");
            resource2 = new IdentityResource(
                name: name2,
                userClaims: new[] { "policy-access", "rd-userid" },
                displayName: "Reactive Domain Access Policy");
            resources.IdentityResources.Add(resource1);
            resources.IdentityResources.Add(resource2);
        }
        [Fact]
        public void can_get_all_resources()
        {
            var allResources = store.GetAllResourcesAsync().Result;
            Assert.Equal(resources.ApiResources, allResources.ApiResources); //should be empty
            Assert.Equal(resources.ApiScopes, allResources.ApiScopes); //should be empty
            Assert.Collection(allResources.IdentityResources,
                    r =>
                    {
                        var resource = Assert.IsType<IdentityResource>(r);
                        Assert.Equal(resource1.Name, resource.Name);
                        Assert.Equal(resource1.DisplayName, resource.DisplayName);
                        Assert.Equal(resource1.UserClaims, resource.UserClaims);
                    },
                    r =>
                    {
                        var resource = Assert.IsType<IdentityResource>(r);
                        Assert.Equal(resource2.Name, resource.Name);
                        Assert.Equal(resource2.DisplayName, resource.DisplayName);
                        Assert.Equal(resource2.UserClaims, resource.UserClaims);
                    });
        }
        [Fact]
        public void can_get_identity_resource_by_name()
        {

            var resources = store.FindIdentityResourcesByScopeNameAsync(new[] { name1 }).Result;
            Assert.Collection(resources,
                   r =>
                   {
                       var resource = Assert.IsType<IdentityResource>(r);
                       Assert.Equal(name1, resource.Name);
                   });
            resources = store.FindIdentityResourcesByScopeNameAsync(new[] { name2 }).Result;
            Assert.Collection(resources,
                   r =>
                   {
                       var resource = Assert.IsType<IdentityResource>(r);
                       Assert.Equal(name2, resource.Name);
                   });
            resources = store.FindIdentityResourcesByScopeNameAsync(new[] { name1, name2 }).Result;
            Assert.Collection(resources,
                   r =>
                   {
                       var resource = Assert.IsType<IdentityResource>(r);
                       Assert.Equal(name1, resource.Name);
                   },
                    r =>
                    {
                        var resource = Assert.IsType<IdentityResource>(r);
                        Assert.Equal(name2, resource.Name);
                    });
            resources = store.FindIdentityResourcesByScopeNameAsync(new[] { name1, name3 }).Result;
            Assert.Collection(resources,
                   r =>
                   {
                       var resource = Assert.IsType<IdentityResource>(r);
                       Assert.Equal(name1, resource.Name);
                   });
        }
        [Fact]
        public void other_resources_not_available() {
            var apiScopes = store.FindApiScopesByNameAsync(new[] { name1, name2, name3 }).Result;
            Assert.Empty(apiScopes);
            var apiResources = store.FindApiResourcesByScopeNameAsync(new[] { name1, name2, name3 }).Result;
            Assert.Empty(apiResources);
            apiResources = store.FindApiResourcesByNameAsync(new[] { name1, name2, name3 }).Result;
            Assert.Empty(apiResources);
        }      
    }
}
