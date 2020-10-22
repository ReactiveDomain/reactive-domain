using System;
using System.Collections.Generic;
using System.Linq;
using ReactiveDomain.Foundation;
using ReactiveDomain.Identity.Storage.Messages;
using ReactiveDomain.Messaging.Bus;

namespace ReactiveDomain.Identity.Storage.Domain.Services
{
    public class ProvidersRM :
        ReadModelBase,
        IHandle<ExternalProviderMsgs.ProviderCreated>
    {
        private string _stream => new PrefixedCamelCaseStreamNameBuilder(Bootstrap.Schema).GenerateForEventType(nameof(ExternalProviderMsgs.ProviderCreated));
        public ProvidersRM(Func<string, IListener> getListener)
            : base(
                nameof(ProvidersRM),
                () => getListener(nameof(UsersRM)))
        {
            // ReSharper disable once RedundantTypeArgumentsOfMethod
            EventStream.Subscribe<ExternalProviderMsgs.ProviderCreated>(this);
            Start(_stream, blockUntilLive: true);
        }

        public bool TryGetProviderId(string providerName, out Guid id)
        {
            if (_providers.ContainsValue(providerName))
            {
                id = _providers.First(x => x.Value == providerName).Key;
                return true;
            }
            id = Guid.Empty;
            return false;
        }

        private readonly Dictionary<Guid, string> _providers = new Dictionary<Guid, string>();

        public void Handle(ExternalProviderMsgs.ProviderCreated message)
        {
            _providers.Add(message.ProviderId, message.ProviderName);
        }
    }
}
