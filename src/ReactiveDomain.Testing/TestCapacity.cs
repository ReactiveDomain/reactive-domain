namespace ReactiveDomain.Testing;

/// <summary>
/// How much CPU a test process has to work with, bucketed. <see cref="TestTimeouts"/> picks its
/// wait budgets from this.
/// </summary>
/// <remarks>
/// A wait fails early because the machine could not schedule the work in time, which depends on
/// cores — so cores choose the bucket, and a CI runner is whichever size its cores make it.
/// </remarks>
public enum TestCapacity {
	/// <summary>Too few cores to run a suite in parallel; the scheduler sets the pace.</summary>
	Small,

	/// <summary>Enough cores to make progress, few enough that a parallel suite contends.</summary>
	Medium,

	/// <summary>Cores to spare, so an expired wait means something is actually wrong.</summary>
	Large,
}
