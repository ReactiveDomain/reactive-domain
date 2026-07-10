// ReSharper disable MemberCanBePrivate.Global

using ReactiveDomain.Logging;

namespace ReactiveDomain.Util;

public enum ExitCode {
	Success = 0,
	Error = 1
}

public class Application {
	public const string AdditionalCommitChecks = "ADDITIONAL_COMMIT_CHECKS";
	public const string InfiniteMetastreams = "INFINITE_METASTREAMS";
	public const string DumpStatistics = "DUMP_STATISTICS";
	public const string DoNotTimeoutRequests = "DO_NOT_TIMEOUT_REQUESTS";
	public const string AlwaysKeepScavenged = "ALWAYS_KEEP_SCAVENGED";
	public const string DisableMergeChunks = "DISABLE_MERGE_CHUNKS";

	protected static readonly ILogger Log = LogManager.GetLogger("ReactiveDomain");

	private static Action<int>? _onExit;
	private static int _exited;

	private static readonly HashSet<string> _defines = [];

	public static void RegisterExitAction(Action<int> exitAction) {
		Ensure.NotNull(exitAction, nameof(exitAction));

		_onExit = exitAction;
	}

	public static void ExitSilent(int exitCode, string reason) {
		Exit(exitCode, reason, silent: true);
	}

	public static void Exit(ExitCode exitCode, string reason) {
		Exit((int)exitCode, reason);
	}

	public static void Exit(int exitCode, string reason) {
		Exit(exitCode, reason, silent: false);
	}

	private static void Exit(int exitCode, string reason, bool silent) {
		if (Interlocked.CompareExchange(ref _exited, 1, 0) != 0)
			return;

		Ensure.NotNullOrEmpty(reason, nameof(reason));

		if (!silent) {
			var message = $"Exiting with exit code: {exitCode}.\nExit reason: {reason}";
			Console.WriteLine(message);
			if (exitCode != 0)
				Log.Error(message);
			else
				Log.Info(message);
		}

		_onExit?.Invoke(exitCode);
	}

	public static void AddDefines(IEnumerable<string> defines) {
		foreach (var define in defines.Safe()) {
			_defines.Add(define.ToUpper());
		}
	}

	public static bool IsDefined(string define) {
		Ensure.NotNull(define, nameof(define));
		return _defines.Contains(define.ToUpper());
	}
}
