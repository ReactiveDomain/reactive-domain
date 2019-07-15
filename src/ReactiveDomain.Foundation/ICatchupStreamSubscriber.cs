using System;
using ReactiveDomain.Messaging;

namespace ReactiveDomain.Foundation
{
    public interface ICatchupStreamSubscriber
    {
        /// <summary>
        /// From EventStore client
        /// Subscribes to a single event stream. Existing events from
        /// lastCheckpoint onwards are read from the stream
        /// and presented to the user of <see cref="T:EventStore.ClientAPI.EventStoreCatchUpSubscription" />
        /// as if they had been pushed.
        /// 
        /// Once the end of the stream is read the subscription is
        /// transparently (to the user) switched to push new events as
        /// they are written.
        /// 
        /// The action liveProcessingStarted is called when the
        /// <see cref="T:EventStore.ClientAPI.EventStoreCatchUpSubscription" /> switches from the reading
        /// phase to the live subscription phase.
        /// </summary>
        /// <param name="stream">The stream to subscribe to</param>
        /// <param name="lastCheckpoint">The event number from which to start.
        /// 
        /// To receive all events in the stream, use <see cref="F:EventStore.ClientAPI.StreamCheckpoint.StreamStart" />.
        /// If events have already been received and resubscription from the same point
        /// is desired, use the event number of the last event processed which
        /// appeared on the subscription.
        /// 
        /// NOTE: Using <see cref="F:EventStore.ClientAPI.StreamPosition.Start" /> here will result in missing
        /// the first event in the stream.</param>
        /// <param name="resolveLinkTos">Whether to resolve Link events automatically</param>
        /// <param name="eventAppeared">An action invoked when an event is received over the subscription</param>
        /// <param name="liveProcessingStarted">An action invoked when the subscription switches to newly-pushed events</param>
        /// <param name="subscriptionDropped">An action invoked if the subscription is dropped</param>
        /// <param name="userCredentials">User credentials to use for the operation</param>
        /// <param name="readBatchSize">The batch size to use during the read phase</param>
        /// <returns>An <see cref="T:EventStore.ClientAPI.EventStoreSubscription" /> representing the subscription</returns>
        IDisposable SubscribeToStreamFrom(
            string stream, 
            int? lastCheckpoint, 
            bool resolveLinkTos, 
            Action<IMessage> eventAppeared, 
            Action liveProcessingStarted = null, 
            Action<SubscriptionDropReason, Exception> subscriptionDropped = null, 
            UserCredentials userCredentials = null, 
            int readBatchSize = 500);

        /// <summary>
        /// Allow pre-validation of stream names
        /// </summary>
        /// <param name="streamName">The name of the target stream</param>
        /// <returns></returns>
        bool ValidateStreamName(string streamName);
    }
}