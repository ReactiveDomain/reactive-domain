namespace ReactiveDomain.Testing;

/// <summary>
/// Decides which <see cref="TestExecutionProfile"/> a test process runs under, and reports the
/// signal it decided on. <see cref="TestTimeouts"/> reads this to pick its budgets.
/// </summary>
/// <remarks>
/// <para>
/// The gap this closes: keying only on <c>GITHUB_ACTIONS</c> means a container or agent sandbox
/// that forgets to export it silently gets the fast local budget despite CI-grade CPU
/// contention, which surfaces as timeouts that look like product flakes. Detection is widened
/// to the CI systems that advertise themselves, plus a small list of named container markers.
/// </para>
/// <para>
/// Detection is deliberately asymmetric: an explicit override always wins, and absent one only
/// signals that unambiguously identify a CI runner or a known constrained sandbox are trusted —
/// anything else is <see cref="TestExecutionProfile.Local"/>. A too-generous CI budget hides
/// nothing an isolated re-run cannot still catch, while a too-tight local budget manufactures
/// flakes.
/// </para>
/// </remarks>
public static class TestExecutionProfileDetector {
	/// <summary>
	/// Explicit override. Recognized values are <c>"Ci"</c> and <c>"Local"</c>
	/// (case-insensitive); any other value is treated as unset and falls through to
	/// auto-detection.
	/// </summary>
	public const string OverrideEnvironmentVariable = "REACTIVEDOMAIN_TEST_PROFILE";

	/// <summary>
	/// A recognized alias for <see cref="OverrideEnvironmentVariable"/>, kept so consumers that
	/// already set this variable against their own copy of this detector keep working. Consulted
	/// only when <see cref="OverrideEnvironmentVariable"/> is unset or unrecognized.
	/// </summary>
	public const string AliasOverrideEnvironmentVariable = "POWERMODELS_TEST_PROFILE";

	// CI systems that set their own variable to the literal "true". GITHUB_ACTIONS is checked
	// first for continuity with the behavior this widens.
	private static readonly string[] BooleanCiEnvironmentVariables = [
		"GITHUB_ACTIONS",
		"CI",         // generic convention (npm's ci-info, GitLab, CircleCI, Travis, ...)
		"TF_BUILD",   // Azure Pipelines
		"BUILDKITE",  // Buildkite
		"GITLAB_CI",  // GitLab CI
		"CIRCLECI",   // CircleCI
	];

	// CI systems that signal by setting a non-boolean variable (a URL, a version string) —
	// presence, not value, is the signal.
	private static readonly string[] PresenceCiEnvironmentVariables = [
		"JENKINS_URL",
		"TEAMCITY_VERSION",
	];

	// Container indicators for the documented gap: a container run with none of the CI
	// variables above set. These name specific, known sandboxes rather than a fuzzy heuristic
	// like "few CPUs visible", which could misfire on an ordinary developer laptop.
	private static readonly string[] ContainerIndicatorEnvironmentVariables = [
		"CLAUDECODE",         // set in every Claude Code process
		"CLAUDE_CODE_REMOTE", // set for cloud/remote Claude Code sessions
	];

	/// <summary>The profile this process detected, computed once from the real environment.</summary>
	public static TestExecutionProfile Detected { get; }

	/// <summary>Human-readable reason <see cref="Detected"/> was chosen — the signal that fired.</summary>
	public static string Reason { get; }

	static TestExecutionProfileDetector() => (Detected, Reason) = Detect(Environment.GetEnvironmentVariable);

	/// <summary>
	/// Pure detection core — takes an environment-variable lookup instead of reading the real
	/// environment, so every branch can be exercised deterministically.
	/// </summary>
	/// <param name="getEnvironmentVariable">Returns the value of the named variable, or null.</param>
	/// <returns>The detected profile and the signal that chose it.</returns>
	public static (TestExecutionProfile Profile, string Reason) Detect(Func<string, string?> getEnvironmentVariable) {
		if (TryReadOverride(getEnvironmentVariable, OverrideEnvironmentVariable, out var overridden))
			return overridden;
		if (TryReadOverride(getEnvironmentVariable, AliasOverrideEnvironmentVariable, out overridden))
			return overridden;

		foreach (var name in BooleanCiEnvironmentVariables) {
			if (string.Equals(getEnvironmentVariable(name), "true", StringComparison.OrdinalIgnoreCase))
				return (TestExecutionProfile.Ci, $"{name}=true");
		}

		foreach (var name in PresenceCiEnvironmentVariables) {
			if (!string.IsNullOrEmpty(getEnvironmentVariable(name)))
				return (TestExecutionProfile.Ci, $"{name} is set");
		}

		foreach (var name in ContainerIndicatorEnvironmentVariables) {
			if (!string.IsNullOrEmpty(getEnvironmentVariable(name)))
				return (TestExecutionProfile.Ci, $"{name} is set (agent container)");
		}

		return (TestExecutionProfile.Local, "no override/CI/container signal found");
	}

	private static bool TryReadOverride(
		Func<string, string?> getEnvironmentVariable,
		string variable,
		out (TestExecutionProfile Profile, string Reason) result) {
		var value = getEnvironmentVariable(variable);
		if (!string.IsNullOrWhiteSpace(value)) {
			foreach (var profile in new[] { TestExecutionProfile.Ci, TestExecutionProfile.Local }) {
				if (string.Equals(value, profile.ToString(), StringComparison.OrdinalIgnoreCase)) {
					result = (profile, $"{variable}={profile} (explicit override)");
					return true;
				}
			}
			// Unrecognized value: not a silent no-op — fall through to auto-detection, where a
			// genuine CI signal still wins. Claiming an override took effect when it did not
			// understand its own value would be the more surprising failure mode.
		}

		result = default;
		return false;
	}
}
