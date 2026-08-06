namespace ReactiveDomain.Testing;

/// <summary>
/// The three wait budgets a test run uses. Defaults per <see cref="TestCapacity"/> come from
/// <see cref="For"/>; a consuming project that has measured its own suite replaces them via
/// <see cref="TestTimeouts.Budget"/>.
/// </summary>
/// <param name="WaitFor">Message-arrival waits: <see cref="TestQueue.WaitFor{T}"/>,
/// <see cref="TestQueue.WaitForMsgId"/>, RepositoryEvents.</param>
/// <param name="CommandTimeout">Command-response waits (dispatcher Send).</param>
/// <param name="ThrottleWaitFor">Waits on real-time Rx operators (Throttle, Buffer, Sample),
/// whose timers run on wall-clock schedulers.</param>
public sealed record TestTimeoutBudget(TimeSpan WaitFor, TimeSpan CommandTimeout, TimeSpan ThrottleWaitFor) {
	/// <summary>
	/// The shipped default for a bucket — a starting point, not a measurement. A suite that trips
	/// these is telling you what its own numbers should be; set them and move on.
	/// </summary>
	public static TestTimeoutBudget For(TestCapacity capacity) => capacity switch {
		TestCapacity.Large => new(TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(2)),
		TestCapacity.Medium => new(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5)),
		_ => new(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10)),
	};
}
