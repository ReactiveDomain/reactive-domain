using System;
using System.Collections.Generic;
using System.Linq;
using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Users.Messages;

namespace ReactiveDomain.Users.Domain.Services
{
    public class ProvidersRM :
        ReadModelBase,
        IHandle<ExternalProviderMsgs.ProviderCreated>
    {
        private readonly string _stream; 
        public ProvidersRM(
            string schema,
            Func<IListener> getListener)
            : base(nameof(ProvidersRM),getListener)
        {
            // ReSharper disable once RedundantTypeArgumentsOfMethod
            EventStream.Subscribe<ExternalProviderMsgs.ProviderCreated>(this);
            _stream = new PrefixedCamelCaseStreamNameBuilder(schema).GenerateForEventType(nameof(ExternalProviderMsgs.ProviderCreated));
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
