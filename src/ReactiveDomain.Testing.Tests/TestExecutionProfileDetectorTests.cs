using Xunit;

namespace ReactiveDomain.Testing.Tests;

public sealed class TestExecutionProfileDetectorTests {
	// A pure lookup over an in-memory map, so every branch of Detect() is exercised
	// deterministically without mutating real process environment variables — which are shared,
	// order-dependent state across every test in the process.
	private static Func<string, string?> Env(params (string Name, string Value)[] vars) {
		var map = vars.ToDictionary(v => v.Name, v => v.Value, StringComparer.OrdinalIgnoreCase);
		return name => map.TryGetValue(name, out var value) ? value : null;
	}

	[Fact]
	public void no_signals_at_all_yields_local() {
		var (profile, reason) = TestExecutionProfileDetector.Detect(Env());

		Assert.Equal(TestExecutionProfile.Local, profile);
		Assert.Contains("no override/CI/container signal", reason);
	}

	[Fact]
	public void github_actions_is_still_detected() {
		var (profile, reason) = TestExecutionProfileDetector.Detect(Env(("GITHUB_ACTIONS", "true")));

		Assert.Equal(TestExecutionProfile.Ci, profile);
		Assert.Contains("GITHUB_ACTIONS", reason);
	}

	[Theory]
	[InlineData("CI")]
	[InlineData("TF_BUILD")]
	[InlineData("BUILDKITE")]
	[InlineData("GITLAB_CI")]
	[InlineData("CIRCLECI")]
	public void other_boolean_ci_variables_are_detected(string variable) {
		var (profile, reason) = TestExecutionProfileDetector.Detect(Env((variable, "true")));

		Assert.Equal(TestExecutionProfile.Ci, profile);
		Assert.Contains(variable, reason);
	}

	[Theory]
	[InlineData("GITHUB_ACTIONS")]
	[InlineData("CI")]
	public void boolean_ci_variables_set_to_false_are_not_a_signal(string variable) {
		var (profile, _) = TestExecutionProfileDetector.Detect(Env((variable, "false")));

		Assert.Equal(TestExecutionProfile.Local, profile);
	}

	[Theory]
	[InlineData("JENKINS_URL", "https://jenkins.example/job/1")]
	[InlineData("TEAMCITY_VERSION", "2023.05")]
	public void presence_only_ci_variables_are_detected(string variable, string value) {
		var (profile, reason) = TestExecutionProfileDetector.Detect(Env((variable, value)));

		Assert.Equal(TestExecutionProfile.Ci, profile);
		Assert.Contains(variable, reason);
	}

	[Theory]
	[InlineData("CLAUDECODE", "1")]
	[InlineData("CLAUDE_CODE_REMOTE", "true")]
	public void container_markers_are_detected(string variable, string value) {
		var (profile, reason) = TestExecutionProfileDetector.Detect(Env((variable, value)));

		Assert.Equal(TestExecutionProfile.Ci, profile);
		Assert.Contains(variable, reason);
	}

	[Fact]
	public void a_ci_variable_outranks_a_container_marker() {
		// Both name the same budget, but the reason should name the stronger signal, since that
		// is what a red run's log has to be read against.
		var (profile, reason) = TestExecutionProfileDetector.Detect(
			Env(("GITHUB_ACTIONS", "true"), ("CLAUDECODE", "1")));

		Assert.Equal(TestExecutionProfile.Ci, profile);
		Assert.Contains("GITHUB_ACTIONS", reason);
	}

	[Fact]
	public void explicit_override_to_ci_wins_with_no_other_signal() {
		var (profile, reason) = TestExecutionProfileDetector.Detect(
			Env((TestExecutionProfileDetector.OverrideEnvironmentVariable, "Ci")));

		Assert.Equal(TestExecutionProfile.Ci, profile);
		Assert.Contains("explicit override", reason);
	}

	[Fact]
	public void explicit_override_to_local_wins_even_over_github_actions() {
		var (profile, reason) = TestExecutionProfileDetector.Detect(
			Env(
				(TestExecutionProfileDetector.OverrideEnvironmentVariable, "Local"),
				("GITHUB_ACTIONS", "true")));

		Assert.Equal(TestExecutionProfile.Local, profile);
		Assert.Contains("explicit override", reason);
	}

	[Fact]
	public void explicit_override_to_local_wins_even_over_a_container_marker() {
		var (profile, _) = TestExecutionProfileDetector.Detect(
			Env(
				(TestExecutionProfileDetector.OverrideEnvironmentVariable, "Local"),
				("CLAUDECODE", "1")));

		Assert.Equal(TestExecutionProfile.Local, profile);
	}

	[Theory]
	[InlineData("ci")]
	[InlineData("CI")]
	[InlineData("cI")]
	public void override_values_are_case_insensitive(string value) {
		var (profile, _) = TestExecutionProfileDetector.Detect(
			Env((TestExecutionProfileDetector.OverrideEnvironmentVariable, value)));

		Assert.Equal(TestExecutionProfile.Ci, profile);
	}

	[Fact]
	public void unrecognized_override_value_falls_through_to_auto_detection() {
		var (profile, reason) = TestExecutionProfileDetector.Detect(
			Env(
				(TestExecutionProfileDetector.OverrideEnvironmentVariable, "banana"),
				("GITHUB_ACTIONS", "true")));

		Assert.Equal(TestExecutionProfile.Ci, profile);
		Assert.DoesNotContain("override", reason);
	}

	[Fact]
	public void the_alias_override_variable_is_recognized() {
		var (profile, reason) = TestExecutionProfileDetector.Detect(
			Env((TestExecutionProfileDetector.AliasOverrideEnvironmentVariable, "Local")));

		Assert.Equal(TestExecutionProfile.Local, profile);
		Assert.Contains(TestExecutionProfileDetector.AliasOverrideEnvironmentVariable, reason);
	}

	[Fact]
	public void the_alias_override_variable_outranks_a_ci_signal() {
		var (profile, _) = TestExecutionProfileDetector.Detect(
			Env(
				(TestExecutionProfileDetector.AliasOverrideEnvironmentVariable, "Local"),
				("GITHUB_ACTIONS", "true")));

		Assert.Equal(TestExecutionProfile.Local, profile);
	}

	[Fact]
	public void the_native_override_variable_outranks_the_alias() {
		var (profile, reason) = TestExecutionProfileDetector.Detect(
			Env(
				(TestExecutionProfileDetector.OverrideEnvironmentVariable, "Ci"),
				(TestExecutionProfileDetector.AliasOverrideEnvironmentVariable, "Local")));

		Assert.Equal(TestExecutionProfile.Ci, profile);
		Assert.Contains(TestExecutionProfileDetector.OverrideEnvironmentVariable, reason);
	}

	[Fact]
	public void an_unrecognized_native_override_falls_through_to_the_alias() {
		var (profile, reason) = TestExecutionProfileDetector.Detect(
			Env(
				(TestExecutionProfileDetector.OverrideEnvironmentVariable, "banana"),
				(TestExecutionProfileDetector.AliasOverrideEnvironmentVariable, "Ci")));

		Assert.Equal(TestExecutionProfile.Ci, profile);
		Assert.Contains(TestExecutionProfileDetector.AliasOverrideEnvironmentVariable, reason);
	}

	[Fact]
	public void a_whitespace_override_is_treated_as_unset() {
		var (profile, reason) = TestExecutionProfileDetector.Detect(
			Env((TestExecutionProfileDetector.OverrideEnvironmentVariable, "   ")));

		Assert.Equal(TestExecutionProfile.Local, profile);
		Assert.Contains("no override/CI/container signal", reason);
	}

	[Fact]
	public void the_detected_profile_is_what_test_timeouts_reports() {
		Assert.Equal(TestExecutionProfileDetector.Detected, TestTimeouts.Profile);
		Assert.Equal(TestExecutionProfileDetector.Detected == TestExecutionProfile.Ci, TestTimeouts.IsCi);
		Assert.Equal(TestExecutionProfileDetector.Reason, TestTimeouts.ProfileReason);
	}

	[Fact]
	public void the_profile_description_names_the_profile_the_reason_and_the_budgets() {
		var description = TestTimeouts.ProfileDescription;

		Assert.Contains(TestTimeouts.Profile.ToString(), description);
		Assert.Contains(TestTimeouts.ProfileReason, description);
		Assert.Contains(TestTimeouts.WaitFor.ToString(), description);
		Assert.Contains(TestTimeouts.CommandTimeout.ToString(), description);
		Assert.Contains(TestTimeouts.ThrottleWaitFor.ToString(), description);
	}

	[Fact]
	public void the_profile_can_be_written_to_a_log() {
		using var writer = new StringWriter();

		TestTimeouts.WriteProfile(writer);

		Assert.Contains(TestTimeouts.ProfileDescription, writer.ToString());
	}

	[Fact]
	public void the_budgets_still_match_the_profile() {
		// The detector decides *which* budget applies; it does not change what the budgets are.
		if (TestTimeouts.IsCi) {
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
