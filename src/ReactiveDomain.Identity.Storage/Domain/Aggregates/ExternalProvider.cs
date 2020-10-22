using System;
using Elbe.Messages;
using ReactiveDomain;
using ReactiveDomain.Messaging;
using ReactiveDomain.Util;

namespace Elbe.Domain
{
    public class ExternalProvider : AggregateRoot
    {
        private ExternalProvider()
        {
            RegisterEvents();
        }

        private void RegisterEvents()
        {
            Register<ExternalProviderMsgs.ProviderCreated>(Apply);
        }

        private void Apply(ExternalProviderMsgs.ProviderCreated message)
        {
            Id = message.ProviderId;
        }

        public ExternalProvider(
            Guid id,
            string providerName,
            ICorrelatedMessage source)
            : this()
        {
            Ensure.NotEmptyGuid(id, nameof(id));
            Ensure.NotNullOrEmpty(providerName, nameof(providerName));

            Ensure.NotNull(source, nameof(source));
            Ensure.NotEmptyGuid(source.CorrelationId, nameof(source.CorrelationId));
            if (source.CausationId == Guid.Empty)
                Ensure.NotEmptyGuid(source.MsgId, nameof(source.MsgId));

            ((ICorrelatedEventSource)this).Source = source;
            Raise(new ExternalProviderMsgs.ProviderCreated(id, providerName));
        }

        /// <summary>
        /// Log the fact that a user has not been successfully authenticated because invalid credentials were supplied.
        /// </summary>
        public void NotAuthenticatedInvalidCredentials(string hostIPAddress)
        {
            Raise(new ExternalProviderMsgs.AuthenticationFailedInvalidCredentials(
                        Id,
                        DateTime.UtcNow,
                        hostIPAddress));
        }
    }
}
