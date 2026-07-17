using Xunit;

namespace ReactiveDomain.Testing.Tests;

public sealed class TestTimeoutsTests {
	[Fact]
	public void values_match_the_environment() {
		var isCi = string.Equals(
			Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), "true", StringComparison.OrdinalIgnoreCase);

		Assert.Equal(isCi, TestTimeouts.IsCi);
		if (isCi) {
			Assert.Equal(TimeSpan.FromSeconds(5), TestTimeouts.WaitFor);
			Assert.Equal(TimeSpan.FromSeconds(10), TestTimeouts.CommandTimeout);
			Assert.Equal(TimeSpan.FromSeconds(10), TestTimeouts.ThrottleWaitFor);
		} else {
			Assert.Equal(TimeSpan.FromMilliseconds(500), TestTimeouts.WaitFor);
			Assert.Equal(TimeSpan.FromMilliseconds(500), TestTimeouts.CommandTimeout);
			Assert.Equal(TimeSpan.FromSeconds(2), TestTimeouts.ThrottleWaitFor);
		}
	}
}
