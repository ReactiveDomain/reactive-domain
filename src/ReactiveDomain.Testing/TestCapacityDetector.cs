namespace ReactiveDomain.Testing;

/// <summary>
/// Buckets a test process by the cores available to it, floored at <see cref="TestCapacity.Small"/>
/// on a recognized CI environment, and reports what it decided on.
/// </summary>
/// <remarks>
/// <para>Cores are the primary signal because cores are the usual cause: a wait that expires early
/// did so because the machine could not schedule the work in time.
/// <see cref="Environment.ProcessorCount"/> already reflects a container's CPU cap.</para>
/// <para>CI is the exception cores cannot see: a hosted runner's cores say nothing about noisy
/// neighbours, cold caches, or suites whose waits price I/O rather than scheduling — so a
/// recognized CI variable keeps the widest budgets regardless of core count, and
/// <see cref="OverrideEnvironmentVariable"/> is the escape hatch in both directions.</para>
/// </remarks>
public static class TestCapacityDetector {
	/// <summary>
	/// Forces a bucket regardless of cores or CI, for when neither is the whole story — a
	/// many-core machine running several test jobs at once has the cores but not the capacity.
	/// Recognized values are the <see cref="TestCapacity"/> names, case-insensitively.
	/// </summary>
	public const string OverrideEnvironmentVariable = "REACTIVEDOMAIN_TEST_CAPACITY";

	/// <summary>
	/// The variables whose value <c>true</c> or <c>1</c> marks a CI environment: <c>GITHUB_ACTIONS</c>,
	/// and the generic <c>CI</c> most providers set. A CI system off this list sets
	/// <see cref="OverrideEnvironmentVariable"/> instead.
	/// </summary>
	public static readonly IReadOnlyList<string> CiEnvironmentVariables = ["GITHUB_ACTIONS", "CI"];

	/// <summary>Default lower bound for <see cref="TestCapacity.Large"/>.</summary>
	public const int LargeCores = 8;

	/// <summary>Default lower bound for <see cref="TestCapacity.Medium"/>; below it is Small.</summary>
	public const int MediumCores = 4;

	/// <summary>The bucket this process detected, computed once.</summary>
	public static TestCapacity Detected { get; }

	/// <summary>Cores this process can use — a container's cap, not the host's total.</summary>
	public static int Cores { get; }

	/// <summary>What chose <see cref="Detected"/>, so a red run can state the budget it used.</summary>
	public static string Reason { get; }

	static TestCapacityDetector() =>
		(Detected, Cores, Reason) = Detect(Environment.ProcessorCount, Environment.GetEnvironmentVariable);

	/// <summary>
	/// Pure detection core — takes the core count and an environment lookup rather than reading the
	/// process, so every branch is exercisable without mutating state shared by the whole test run.
	/// </summary>
	/// <param name="cores">Cores available to the process.</param>
	/// <param name="getEnvironmentVariable">Returns the named variable's value, or null.</param>
	public static (TestCapacity Capacity, int Cores, string Reason) Detect(
		int cores,
		Func<string, string?> getEnvironmentVariable) {
		var overridden = getEnvironmentVariable(OverrideEnvironmentVariable);
		if (!string.IsNullOrWhiteSpace(overridden)) {
			foreach (var capacity in Enum.GetValues<TestCapacity>()) {
				if (string.Equals(overridden, capacity.ToString(), StringComparison.OrdinalIgnoreCase))
					return (capacity, cores, $"{OverrideEnvironmentVariable}={capacity} (explicit override)");
			}
			// Unrecognized: fall through to the cores rather than honour a default nobody asked for.
		}

		foreach (var variable in CiEnvironmentVariables) {
			var value = getEnvironmentVariable(variable);
			if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || value == "1") {
				return (TestCapacity.Small, cores,
					$"{variable}={value}: CI floors the bucket at {TestCapacity.Small} over {cores} cores " +
					$"({OverrideEnvironmentVariable} overrides)");
			}
		}

		if (cores >= LargeCores)
			return (TestCapacity.Large, cores, $"{cores} cores available, {LargeCores} or more");
		if (cores >= MediumCores)
			return (TestCapacity.Medium, cores, $"{cores} cores available, under {LargeCores}");
		return (TestCapacity.Small, cores, $"{cores} cores available, under {MediumCores}");
	}
}
