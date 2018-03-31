using System;
using ReactiveDomain.Util;

namespace ReactiveDomain
{
    /// <summary>
  /// Settings for <see cref="T:EventStore.ClientAPI.EventStoreCatchUpSubscription" />.
  /// </summary>
  public class CatchUpSubscriptionSettings
  {
    /// <summary>Returns default settings.</summary>
    public static readonly CatchUpSubscriptionSettings Default = new CatchUpSubscriptionSettings(10000, 500, false, string.Empty);
    /// <summary>
    /// The maximum amount of events to cache when processing from a live subscription. Going above this value will drop the subscription.
    /// </summary>
    public readonly int MaxLiveQueueSize;
    /// <summary>
    /// The number of events to read per batch when reading the history.
    /// </summary>
    public readonly int ReadBatchSize;
    /// <summary>Enables verbose logging on the subscription.</summary>
    public readonly bool VerboseLogging;
    /// <summary>The name of the subscription.</summary>
    public readonly string SubscriptionName;

    /// <summary>
    /// Constructs a <see cref="T:EventStore.ClientAPI.CatchUpSubscriptionSettings" /> object.
    /// </summary>
    /// <param name="maxLiveQueueSize">The maximum amount of events to buffer when processing from a live subscription. Going above this amount will drop the subscription.</param>
    /// <param name="readBatchSize">The number of events to read per batch when reading through history.</param>
    /// <param name="verboseLogging">Enables verbose logging on the subscription.</param>
    /// <param name="subscriptionName">The name of the subscription.</param>
    public CatchUpSubscriptionSettings(int maxLiveQueueSize, int readBatchSize, bool verboseLogging, string subscriptionName = "")
    {
      Ensure.Positive(readBatchSize, nameof (readBatchSize));
      Ensure.Positive(maxLiveQueueSize, nameof (maxLiveQueueSize));
      if (readBatchSize > Consts.MaxReadSize)
        throw new ArgumentException(
            $"Read batch size should be less than {(object) Consts.MaxReadSize}. For larger reads you should page.");
      MaxLiveQueueSize = maxLiveQueueSize;
      ReadBatchSize = readBatchSize;
      VerboseLogging = verboseLogging;
      SubscriptionName = subscriptionName;
    }
  }
}
