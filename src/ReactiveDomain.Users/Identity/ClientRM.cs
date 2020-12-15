using System;
using System.Collections.Generic;
using System.Linq;
using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Users.Identity;

namespace Elbe.Domain
{
    /// <summary>
    /// A read model that contains a list of existing applications. 
    /// </summary>
    public class ClientRM :
        ReadModelBase,
        IHandle<ClientApplicationMsgs.CreateClientApplication>,
        IHandle<ClientApplicationMsgs.AccessRoleDefined>,
        IHandle<ClientApplicationMsgs.ClientSecretChanged>
    {
        private readonly List<ClientDTO> _identityClients = new List<ClientDTO>();

        private IReadOnlyList<ClientDTO> Clients { get { return _identityClients; } }

        /// <summary>
        /// Create a read model for getting information about existing applications.
        /// </summary>
        public ClientRM(Func<IListener> getListener)
            : base(nameof(ClientRM), getListener)
        {

            EventStream.Subscribe<ClientApplicationMsgs.CreateClientApplication>(this);
            EventStream.Subscribe<ClientApplicationMsgs.AccessRoleDefined>(this);
            EventStream.Subscribe<ClientApplicationMsgs.ClientSecretChanged>(this);

            Start<ClientApplication>(blockUntilLive: true);
        }


        public void Handle(ClientApplicationMsgs.CreateClientApplication @event)
        {
            _identityClients.Add(new ClientDTO(
                                    @event.ApplicationId,
                                    @event.ApplicationDisplayName,
                                    @event.ClientId,
                                    "",//AccessRole
                                    @event.ClientSecret));
        }
        public void Handle(ClientApplicationMsgs.AccessRoleDefined @event)
        {
            var client = _identityClients.FirstOrDefault(c => c.ClientApplicationId == @event.ClientApplicationId);
            if (client == null)
            {
                //todo: log this, it should never happen
                return;
            }
            client.AccessRole = @event.RoleName;
        }
        public void Handle(ClientApplicationMsgs.ClientSecretChanged @event)
        {
            var client = _identityClients.FirstOrDefault(c => c.ClientApplicationId == @event.ClientApplicationId);
            if (client == null)
            {
                //todo: log this, it should never happen
                return;
            }
            client.ClientSecret = @event.ClientSecret;
        }
        public bool TryGetClient(string clientId, out ClientDTO client)
        {
            client = _identityClients.FirstOrDefault(x => x.ClientId == clientId);
            return client != null;
        }
        public bool hasAccess(SubjectDTO subject, ClientDTO client)
        {
            return subject.Roles.Contains(client.AccessRole, StringComparer.OrdinalIgnoreCase);
        }
    }
}
