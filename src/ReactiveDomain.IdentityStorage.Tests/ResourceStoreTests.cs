using System.Threading.Tasks;
using IdentityServer4.Models;
using ReactiveDomain.IdentityStorage.Services;
using Xunit;

namespace ReactiveDomain.IdentityStorage.Tests;

public class ResourceStoreTests {
	private readonly Resources _resources = new();
	private readonly ResourcesStore _store = new();
	private readonly IdentityResource _resource1;
	private readonly IdentityResource _resource2;
	private const string Name1 = "openid";
	private const string Name2 = "rd-policy";
	private const string Name3 = "other";


	public ResourceStoreTests() {

		_resource1 = new IdentityResource(
			name: Name1,
			userClaims: ["sub"],
			displayName: "Your user identifier");
		_resource2 = new IdentityResource(
			name: Name2,
			userClaims: ["policy-access", "rd-userid"],
			displayName: "Reactive Domain Access Policy");
		_resources.IdentityResources.Add(_resource1);
		_resources.IdentityResources.Add(_resource2);
	}
	[Fact]
	public async Task can_get_all_resources() {
		var allResources = await _store.GetAllResourcesAsync();
		Assert.Equal(_resources.ApiResources, allResources.ApiResources); //should be empty
		Assert.Equal(_resources.ApiScopes, allResources.ApiScopes); //should be empty
		Assert.Collection(allResources.IdentityResources,
			r => {
				var resource = Assert.IsType<IdentityResource>(r);
				Assert.Equal(_resource1.Name, resource.Name);
				Assert.Equal(_resource1.DisplayName, resource.DisplayName);
				Assert.Equal(_resource1.UserClaims, resource.UserClaims);
			},
			r => {
				var resource = Assert.IsType<IdentityResource>(r);
				Assert.Equal(_resource2.Name, resource.Name);
				Assert.Equal(_resource2.DisplayName, resource.DisplayName);
				Assert.Equal(_resource2.UserClaims, resource.UserClaims);
			});
	}
	[Fact]
	public async Task can_get_identity_resource_by_name() {
		var scopedResources = await _store.FindIdentityResourcesByScopeNameAsync([Name1]);
		Assert.Collection(scopedResources,
			r => {
				var resource = Assert.IsType<IdentityResource>(r);
				Assert.Equal(Name1, resource.Name);
			});
		scopedResources = await _store.FindIdentityResourcesByScopeNameAsync([Name2]);
		Assert.Collection(scopedResources,
			r => {
				var resource = Assert.IsType<IdentityResource>(r);
				Assert.Equal(Name2, resource.Name);
			});
		scopedResources = await _store.FindIdentityResourcesByScopeNameAsync([Name1, Name2]);
		Assert.Collection(scopedResources,
			r => {
				var resource = Assert.IsType<IdentityResource>(r);
				Assert.Equal(Name1, resource.Name);
			},
			r => {
				var resource = Assert.IsType<IdentityResource>(r);
				Assert.Equal(Name2, resource.Name);
			});
		scopedResources = await _store.FindIdentityResourcesByScopeNameAsync([Name1, Name3]);
		Assert.Collection(scopedResources,
			r => {
				var resource = Assert.IsType<IdentityResource>(r);
				Assert.Equal(Name1, resource.Name);
			});
	}
	[Fact]
	public async Task other_resources_not_available() {
		var apiScopes = await _store.FindApiScopesByNameAsync([Name1, Name2, Name3]);
		Assert.Empty(apiScopes);
		var apiResources = await _store.FindApiResourcesByScopeNameAsync([Name1, Name2, Name3]);
		Assert.Empty(apiResources);
		apiResources = await _store.FindApiResourcesByNameAsync([Name1, Name2, Name3]);
		Assert.Empty(apiResources);
	}
}
