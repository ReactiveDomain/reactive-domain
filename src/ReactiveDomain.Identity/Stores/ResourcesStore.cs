using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IdentityServer4.Models;
using IdentityServer4.Stores;
using ReactiveDomain.Util;

namespace ReactiveDomain.Identity
{
    public class ResourcesStore : IResourceStore
    {
        private readonly Resources _resources = new Resources();

        public ResourcesStore()
        {
            _resources.IdentityResources.Add(new IdentityResource(
                name: "openid",
                userClaims: new[] { "sub" },
                displayName: "Your user identifier"));
            //_resources.IdentityResources.Add(new IdentityResource(
            //    name: "profile",
            //    userClaims: new[] { "name", "email", "website" },
            //    displayName: "Your profile data"));
            _resources.IdentityResources.Add(new IdentityResource(
                name:"rd-policy",
                userClaims:  new []{"policy-access", "rd-userid"},
                displayName: "Reactive Domain Access Policy"));
        }
        //todo:use hash set operations for comparisons line 39 & 50 Identity resources is a collection

        public async Task<IEnumerable<IdentityResource>> FindIdentityResourcesByScopeNameAsync(
            IEnumerable<string> scopeNames)
        {
            var names = new HashSet<string>(scopeNames);
            var resources = new List<IdentityResource>();
            if (!names.IsEmpty())
            {
                resources.AddRange(_resources.IdentityResources.Where(r => names.Contains(r.Name, StringComparer.Ordinal)));
            }
            return await Task.FromResult(resources);
        }

        public async Task<IEnumerable<ApiScope>> FindApiScopesByNameAsync(IEnumerable<string> scopeNames)
        {
            var names = new HashSet<string>(scopeNames);
            var apiScopes = new List<ApiScope>();
            if (!names.IsEmpty())
            {
                apiScopes.AddRange(_resources.ApiScopes.Where(r => names.Contains(r.Name, StringComparer.Ordinal)));
            }
            return await Task.FromResult(apiScopes);
        }

        public async Task<IEnumerable<ApiResource>> FindApiResourcesByScopeNameAsync(IEnumerable<string> scopeNames)
        {
            var names = new HashSet<string>(scopeNames);
            var apiResources = new List<ApiResource>();
            if (!names.IsEmpty())
            {
                foreach (var apiResource in _resources.ApiResources)
                {
                    var scopesHash = new HashSet<string>(apiResource.Scopes);
                    if (scopesHash.Overlaps(names))
                    {
                        apiResources.Add(apiResource);
                    }
                }
            }
            return await Task.FromResult(apiResources);
        }

        public async Task<IEnumerable<ApiResource>> FindApiResourcesByNameAsync(IEnumerable<string> apiResourceNames)
        {
            var names = new HashSet<string>(apiResourceNames);
            var apiResources = new List<ApiResource>();
            if (!names.IsEmpty())
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