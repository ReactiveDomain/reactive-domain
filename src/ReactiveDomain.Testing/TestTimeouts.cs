namespace ReactiveDomain.Testing;

/// <summary>
/// Timeout source for test waits, bucketed by the cores available to the process (see
/// <see cref="TestCapacityDetector"/>) so a machine that cannot run the suite in parallel is not
/// held to a budget written for one that can.
/// </summary>
/// <remarks>
/// <para>The shipped budgets are defaults, not measurements. Tune them per project by assigning
/// <see cref="Budget"/> once at startup — an xunit assembly fixture, a module initializer — before
/// any test reads a wait.</para>
/// <para>To reproduce a smaller machine's timing on a larger one, force the bucket and restrict the
/// runner to matching cores, e.g. from cmd:
/// <c>set REACTIVEDOMAIN_TEST_CAPACITY=Small &amp;&amp; start /affinity 3 dotnet test ...</c>.
/// Run test assemblies sequentially (<c>MaxCpuCount=1</c> in a .runsettings file or
/// <c>-maxcpucount:1</c>) — concurrent in-process stores starve the thread pool.
/// See Docs/ci-test-guidance.md.</para>
/// </remarks>
public static class TestTimeouts {
	/// <summary>The detected bucket, and so which defaults <see cref="Budget"/> starts from.</summary>
	public static TestCapacity Capacity { get; } = TestCapacityDetector.Detected;

	/// <summary>What chose <see cref="Capacity"/>, so a red run can state the budget it used.</summary>
	public static string CapacityReason { get; } = TestCapacityDetector.Reason;

	/// <summary>
	/// The budgets in force. Defaults to <see cref="TestTimeoutBudget.For"/> of
	/// <see cref="Capacity"/>; assign to tune a project's own numbers.
	/// </summary>
	/// <remarks>Set it once before any test reads a wait — this is process-wide, and nothing
	/// re-reads a value a wait has already started against.</remarks>
	public static TestTimeoutBudget Budget { get; set; } = TestTimeoutBudget.For(Capacity);

	/// <inheritdoc cref="TestTimeoutBudget.WaitFor"/>
	public static TimeSpan WaitFor => Budget.WaitFor;

	/// <inheritdoc cref="TestTimeoutBudget.CommandTimeout"/>
	public static TimeSpan CommandTimeout => Budget.CommandTimeout;

	/// <inheritdoc cref="TestTimeoutBudget.ThrottleWaitFor"/>
	public static TimeSpan ThrottleWaitFor => Budget.ThrottleWaitFor;

	/// <summary>True at <see cref="TestCapacity.Small"/> — the bucket, not "a CI system is running".</summary>
	public static bool IsCi => Capacity == TestCapacity.Small;

	/// <summary>
	/// One line naming the bucket, what chose it, and the budgets in force — so a red gate can state
	/// which budget the run used instead of leaving it to be guessed.
	/// </summary>
	public static string CapacityDescription =>
		$"ReactiveDomain.Testing capacity: {Capacity} ({CapacityReason}); " +
		$"{nameof(WaitFor)}={WaitFor}, {nameof(CommandTimeout)}={CommandTimeout}, " +
		$"{nameof(ThrottleWaitFor)}={ThrottleWaitFor}";

	/// <summary>Writes <see cref="CapacityDescription"/>, by default to the console.</summary>
	/// <param name="output">Where to write; defaults to <see cref="Console.Out"/>.</param>
	public static void WriteCapacity(TextWriter? output = null) =>
		(output ?? Console.Out).WriteLine(CapacityDescription);
}
