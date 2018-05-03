using System;
namespace ReactiveDomain
{
    internal static class Consts
    {
        public static readonly TimeSpan DefaultReconnectionDelay = TimeSpan.FromMilliseconds(100.0);
        public static readonly TimeSpan DefaultOperationTimeout = TimeSpan.FromSeconds(7.0);
        public static readonly TimeSpan DefaultOperationTimeoutCheckPeriod = TimeSpan.FromSeconds(1.0);
        public static readonly TimeSpan TimerPeriod = TimeSpan.FromMilliseconds(200.0);
        public static readonly int MaxReadSize = 4096;
        public const int DefaultMaxQueueSize = 5000;
        public const int DefaultMaxConcurrentItems = 5000;
        public const int DefaultMaxOperationRetries = 10;
        public const int DefaultMaxReconnections = 10;
        public const bool DefaultRequireMaster = true;
        public const int DefaultMaxClusterDiscoverAttempts = 10;
        public const int DefaultClusterManagerExternalHttpPort = 30778;
        public const int CatchUpDefaultReadBatchSize = 500;
        public const int CatchUpDefaultMaxPushQueueSize = 10000;
    }
}
