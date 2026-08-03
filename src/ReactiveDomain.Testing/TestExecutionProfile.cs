namespace ReactiveDomain.Testing;

/// <summary>
/// The timeout budget a test process runs under. Names the two budgets
/// <see cref="TestTimeouts"/> exposes, so <see cref="TestExecutionProfileDetector"/> can say
/// which one applies and why.
/// </summary>
public enum TestExecutionProfile {
	/// <summary>Fast local budget — an interactive dev loop with no CPU contention.</summary>
	Local,

	/// <summary>
	/// Generous CI budget — GitHub Actions, another recognized CI system, or a known
	/// constrained/shared-CPU sandbox (e.g. an agent container) where the fast local budget
	/// manufactures flakes that are really scheduler starvation.
	/// </summary>
	Ci,
}
