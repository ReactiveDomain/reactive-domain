using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IdentityServer4.Extensions;
using IdentityServer4.Models;
using IdentityServer4.Stores;

namespace ReactiveDomain.IdentityServer4.Storage {
    public class ResourcesStore : IResourceStore
    {
        private readonly Resources _resources = new Resources();

        public ResourcesStore()
        {
            _resources.IdentityResources.Add(new IdentityResources.OpenId());
            _resources.IdentityResources.Add(new IdentityResources.Profile());
            _resources.ApiScopes.Add(new ApiScope("api1", "My API"));
            _resources.ApiScopes.Add(new ApiScope("api2", "My Other API"));
        }


        public async Task<IEnumerable<IdentityResource>> FindIdentityResourcesByScopeNameAsync(
            IEnumerable<string> scopeNames)
        {
            var names = scopeNames.ToHashSet();
            var resources = new List<IdentityResource>();
            if (!names.IsNullOrEmpty())
            {
                resources.AddRange(_resources.IdentityResources.Where(r => names.Contains(r.Name, StringComparer.Ordinal)));
            }
            return await Task.FromResult(resources);
        }

        public async Task<IEnumerable<ApiScope>> FindApiScopesByNameAsync(IEnumerable<string> scopeNames)
        {
            var names = scopeNames.ToHashSet();
            var apiScopes = new List<ApiScope>();
            if (!names.IsNullOrEmpty())
            {
                apiScopes.AddRange(_resources.ApiScopes.Where(r => names.Contains(r.Name, StringComparer.Ordinal)));
            }
            return await Task.FromResult(apiScopes);
        }

        public async Task<IEnumerable<ApiResource>> FindApiResourcesByScopeNameAsync(IEnumerable<string> scopeNames)
        {
            var names = scopeNames.ToHashSet();
            var apiResources = new List<ApiResource>();
            if (!names.IsNullOrEmpty())
            {
                foreach (var apiResource in _resources.ApiResources) {
                    var scopesHash = apiResource.Scopes.ToHashSet();
                    if (scopesHash.Overlaps(names)) {
                        apiResources.Add(apiResource);
                    }
                }
            }
            return await Task.FromResult(apiResources);
        }

        public async Task<IEnumerable<ApiResource>> FindApiResourcesByNameAsync(IEnumerable<string> apiResourceNames)
        {
            var names = apiResourceNames.ToHashSet();
            var apiResources = new List<ApiResource>();
            if (!names.IsNullOrEmpty())
            {
                apiResources.AddRange(_resources.ApiResources.Where(r => names.Contains(r.Name, StringComparer.Ordinal)));
            }
            return await Task.FromResult(apiResources);
        }

        public async Task<Resources> GetAllResourcesAsync()
        {
            return await Task.FromResult(_resources);
        }
    }
}