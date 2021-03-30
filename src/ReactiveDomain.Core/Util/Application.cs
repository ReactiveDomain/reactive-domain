// ReSharper disable MemberCanBePrivate.Global

using System;
using System.Collections.Generic;
using System.Threading;

using Microsoft.Extensions.Logging;

namespace ReactiveDomain.Util
{
    public enum ExitCode
    {
        Success = 0,
        Error = 1
    }

    public class Application
    {
        public const string AdditionalCommitChecks = "ADDITIONAL_COMMIT_CHECKS";
        public const string InfiniteMetastreams = "INFINITE_METASTREAMS";
        public const string DumpStatistics = "DUMP_STATISTICS";
        public const string DoNotTimeoutRequests = "DO_NOT_TIMEOUT_REQUESTS";
        public const string AlwaysKeepScavenged = "ALWAYS_KEEP_SCAVENGED";
        public const string DisableMergeChunks = "DISABLE_MERGE_CHUNKS";

        protected static readonly ILogger Log = Logging.LogProvider.GetLogger("ReactiveDomain");

        private static Action<int> OnExit;
        private static int Exited;

        private static readonly HashSet<string> Defines = new HashSet<string>();

        public static void RegisterExitAction(Action<int> exitAction)
        {
            Ensure.NotNull(exitAction, "exitAction");

            OnExit = exitAction;
        }

        public static void ExitSilent(int exitCode, string reason)
        {
            Exit(exitCode, reason, silent: true);
        }

        public static void Exit(ExitCode exitCode, string reason)
        {
            Exit((int) exitCode, reason);
        }

        public static void Exit(int exitCode, string reason)
        {
            Exit(exitCode, reason, silent: false);
        }

        private static void Exit(int exitCode, string reason, bool silent)
        {
            if (Interlocked.CompareExchange(ref Exited, 1, 0) != 0)
                return;

            Ensure.NotNullOrEmpty(reason, "reason");

            if (!silent)
            {
                var message = string.Format("Exiting with exit code: {0}.\nExit reason: {1}", exitCode, reason);
                Console.WriteLine(message);
                if (exitCode != 0)
                    Log.LogError(message);
                else
                    Log.LogInformation(message);
            }

            var exit = OnExit;
            if (exit != null)
                exit(exitCode);
        }

        public static void AddDefines(IEnumerable<string> defines)
        {
            foreach (var define in defines.Safe())
            {
                Defines.Add(define.ToUpper());
            }
        }

        public static bool IsDefined(string define)
        {
            Ensure.NotNull(define, "define");
            return Defines.Contains(define.ToUpper());
        }
    }
}