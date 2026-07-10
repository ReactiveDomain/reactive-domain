using ReactiveDomain.IdentityStorage.ReadModels;

namespace ReactiveDomain.IdentityStorage.Tests;

internal class MockPrincipal : IPrincipal {
	public string Provider { get; set; } = string.Empty;

	public string Domain { get; set; } = string.Empty;

	public string SId { get; set; } = string.Empty;
}
