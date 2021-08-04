using System;
using DynamicData;
using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Policy.Messages;

namespace ReactiveDomain.Policy.ReadModels
{
    public class SingleRoleRM :
        ReadModelBase,
        IHandle<ApplicationMsgs.PolicyCreated>,
        IHandle<ApplicationMsgs.RoleCreated>,
        IHandle<PolicyUserMsgs.RoleAdded>,
        IHandle<PolicyUserMsgs.RoleRemoved>
    {
        private readonly string _clientId;
        private readonly string _roleName;
        private Guid _policyId;
        private Guid _roleId;

        public IObservableList<Guid> UsersInRole => _usersInRole;
        private readonly SourceList<Guid> _usersInRole = new SourceList<Guid>();

        public SingleRoleRM(
            string clientId,
            string roleName,
            IConfiguredConnection conn)
            : base(nameof(SingleRoleRM), () => conn.GetListener(nameof(SingleRoleRM)))
        {
            _clientId = clientId;
            _roleName = roleName;

            using (var reader = conn.GetReader(nameof(SingleRoleRM), this))
            {
                reader.EventStream.Subscribe<ApplicationMsgs.PolicyCreated>(this);
                reader.EventStream.Subscribe<ApplicationMsgs.RoleCreated>(this);
                reader.Read<Domain.SecuredApplication>();
            }

            EventStream.Subscribe<PolicyUserMsgs.RoleAdded>(this);
            EventStream.Subscribe<PolicyUserMsgs.RoleRemoved>(this);
            Start<Domain.PolicyUser>();
        }

        public void Handle(ApplicationMsgs.PolicyCreated message)
        {
            if (message.ClientId == _clientId)
                _policyId = message.PolicyId;
        }

        public void Handle(ApplicationMsgs.RoleCreated message)
        {
            if (message.PolicyId == _policyId && message.Name == _roleName)
                _roleId = message.RoleId;
        }

        public void Handle(PolicyUserMsgs.RoleAdded message)
        {
            if (message.RoleId == _roleId)
                _usersInRole.Add(message.PolicyUserId);
        }

        public void Handle(PolicyUserMsgs.RoleRemoved message)
        {
            if (message.RoleId == _roleId)
                _usersInRole.Remove(message.PolicyUserId);
        }
    }
}
