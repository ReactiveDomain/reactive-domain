namespace ReactiveDomain.Testing;

/// <summary>
/// CI-aware timeout source for test waits. The profile is detected by
/// <see cref="TestExecutionProfileDetector"/> — an explicit override, then a known CI system,
/// then a known container marker, else local. CI runners (typically 2 cores) and agent
/// containers suffer scheduler starvation that surfaces as spurious timeouts when tests use
/// locally-tuned values, so waits get generous CI values while staying fast for the local
/// edit-test loop.
/// </summary>
/// <remarks>
/// To reproduce CI-only timing failures locally, launch the test runner restricted to two
/// cores with the profile forced, e.g. from cmd:
/// <c>set REACTIVEDOMAIN_TEST_PROFILE=Ci &amp;&amp; start /affinity 3 dotnet test ...</c>
/// (<c>set GITHUB_ACTIONS=true</c> still works). The same variable set to <c>Local</c> forces
/// the fast budget where detection would choose CI.
/// Run test assemblies sequentially (<c>MaxCpuCount=1</c> in a .runsettings file or
/// <c>-maxcpucount:1</c>) — concurrent in-process stores starve the thread pool.
/// See Docs/ci-test-guidance.md.
/// </remarks>
public static class TestTimeouts {
	/// <summary>The detected execution profile, and so which budgets below are in force.</summary>
	public static TestExecutionProfile Profile { get; } = TestExecutionProfileDetector.Detected;

	/// <summary>The signal that chose <see cref="Profile"/>, so a red run can state its budget.</summary>
	public static string ProfileReason { get; } = TestExecutionProfileDetector.Reason;

	/// <summary>
	/// True when the generous CI budget is in force: under GitHub Actions or another recognized
	/// CI system, in a recognized container, or forced by
	/// <see cref="TestExecutionProfileDetector.OverrideEnvironmentVariable"/>.
	/// </summary>
	public static bool IsCi { get; } = Profile == TestExecutionProfile.Ci;

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

	/// <summary>
	/// One line naming the profile, the signal that chose it, and the budgets in force — so a
	/// red gate can state which budget the run used instead of leaving it to be guessed.
	/// </summary>
	public static string ProfileDescription =>
		$"ReactiveDomain.Testing test execution profile: {Profile} ({ProfileReason}); " +
		$"{nameof(WaitFor)}={WaitFor}, {nameof(CommandTimeout)}={CommandTimeout}, " +
		$"{nameof(ThrottleWaitFor)}={ThrottleWaitFor}";

	/// <summary>Writes <see cref="ProfileDescription"/>, by default to the console.</summary>
	/// <param name="output">Where to write; defaults to <see cref="Console.Out"/>.</param>
	public static void WriteProfile(TextWriter? output = null) =>
		(output ?? Console.Out).WriteLine(ProfileDescription);
}
