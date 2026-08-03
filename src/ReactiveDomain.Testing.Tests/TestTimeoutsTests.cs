using Xunit;

namespace ReactiveDomain.Testing.Tests;

public sealed class TestTimeoutsTests {
	/// <summary>
	/// The budgets agree with the detected profile.
	/// <para>This assertion used to derive the expected profile from <c>GITHUB_ACTIONS</c> alone, which
	/// made "CI" mean exactly one signal. Detection now recognises an explicit override, several CI
	/// providers, and container markers, so that derivation is no longer the contract — a container
	/// with no CI variable is correctly CI, and the old form failed there. What the test was really
	/// protecting is the pairing: whatever profile is detected, the three budgets match it. That is
	/// asserted here against <see cref="TestTimeouts.IsCi"/> itself.</para>
	/// </summary>
	[Fact]
	public void values_match_the_detected_profile() {
		var isCi = TestTimeouts.IsCi;

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

	/// <summary>
	/// The narrow contract still holds in the direction that matters: GitHub Actions is still CI.
	/// Widening detection made that signal sufficient rather than necessary, and this pins the half
	/// that must never regress — RD's own CI would silently drop to local budgets if it did.
	/// </summary>
	[Fact]
	public void github_actions_still_means_ci() {
		if (!string.Equals(
				Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), "true", StringComparison.OrdinalIgnoreCase))
			return;

		Assert.True(TestTimeouts.IsCi);
	}
}
