namespace ReactiveDomain.Testing;

/// <summary>
/// Buckets a test process by the cores available to it, and reports what it decided on.
/// </summary>
/// <remarks>
/// Cores are the signal because cores are the cause: a wait that expires early did so because the
/// machine could not schedule the work in time. Naming CI providers answered that by proxy, and
/// answered it wrong for everything off the list — a capped container, a self-hosted runner, an old
/// laptop. <see cref="Environment.ProcessorCount"/> already reflects a container's CPU cap.
/// </remarks>
public static class TestCapacityDetector {
	/// <summary>
	/// Forces a bucket regardless of cores, for when the core count is not the whole story — a
	/// many-core machine running several test jobs at once has the cores but not the capacity.
	/// Recognized values are the <see cref="TestCapacity"/> names, case-insensitively.
	/// </summary>
	public const string OverrideEnvironmentVariable = "REACTIVEDOMAIN_TEST_CAPACITY";

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

		if (cores >= LargeCores)
			return (TestCapacity.Large, cores, $"{cores} cores available, {LargeCores} or more");
		if (cores >= MediumCores)
			return (TestCapacity.Medium, cores, $"{cores} cores available, under {LargeCores}");
		return (TestCapacity.Small, cores, $"{cores} cores available, under {MediumCores}");
	}
}
