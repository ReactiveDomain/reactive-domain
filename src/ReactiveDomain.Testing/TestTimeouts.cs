namespace ReactiveDomain.Testing;

/// <summary>
/// CI-aware timeout source for test waits, keyed on the <c>GITHUB_ACTIONS</c> environment
/// variable. CI runners (typically 2 cores) suffer scheduler starvation that surfaces as
/// spurious timeouts when tests use locally-tuned values, so waits get generous CI values
/// while staying fast for the local edit-test loop.
/// </summary>
/// <remarks>
/// To reproduce CI-only timing failures locally, launch the test runner restricted to two
/// cores with the CI flag set, e.g. from cmd:
/// <c>set GITHUB_ACTIONS=true &amp;&amp; start /affinity 3 dotnet test ...</c>.
/// Run test assemblies sequentially (<c>MaxCpuCount=1</c> in a .runsettings file or
/// <c>-maxcpucount:1</c>) — concurrent in-process stores starve the thread pool.
/// See Docs/ci-test-guidance.md.
/// </remarks>
public static class TestTimeouts {
	/// <summary>
	/// True when running under GitHub Actions (<c>GITHUB_ACTIONS</c> = "true"), or under the
	/// local repro recipe that sets the same variable.
	/// </summary>
	public static bool IsCi { get; } =
		string.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), "true", StringComparison.OrdinalIgnoreCase);

	/// <summary>
	/// Timeout for message-arrival waits: <see cref="TestQueue.WaitFor{T}"/>,
	/// <see cref="TestQueue.WaitForMsgId"/>, and RepositoryEvents waits.
	/// 500 ms locally, 5 s on CI.
	/// </summary>
	public static TimeSpan WaitFor { get; } = IsCi ? TimeSpan.FromSeconds(5) : TimeSpan.FromMilliseconds(500);

	/// <summary>
	/// Timeout for command-response waits (dispatcher Send). 500 ms locally, 10 s on CI.
	/// </summary>
	public static TimeSpan CommandTimeout { get; } = IsCi ? TimeSpan.FromSeconds(10) : TimeSpan.FromMilliseconds(500);

	/// <summary>
	/// Timeout for waits on real-time Rx operators (Throttle, Buffer, Sample) whose timers
	/// run on wall-clock schedulers. 2 s locally, 10 s on CI.
	/// </summary>
	public static TimeSpan ThrottleWaitFor { get; } = IsCi ? TimeSpan.FromSeconds(10) : TimeSpan.FromSeconds(2);
}
