using DynamicData;
using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Policy.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ReactiveDomain.Policy.ReadModels
{
    public class FilteredPoliciesRM :
        ReadModelBase,
        IHandle<ApplicationMsgs.ApplicationCreated>,
        IHandle<ApplicationMsgs.PolicyCreated>,
        IHandle<ApplicationMsgs.RoleCreated>,
        IHandle<PolicyUserMsgs.PolicyUserAdded>,
        IHandle<PolicyUserMsgs.RoleAdded>,
        IHandle<PolicyUserMsgs.RoleRemoved>,
        IHandle<PolicyUserMsgs.UserDeactivated>,
        IHandle<PolicyUserMsgs.UserReactivated>
    {

        public IConnectableCache<PolicyDTO, Guid> Polices => _polices;
        private readonly SourceCache<PolicyDTO, Guid> _polices = new SourceCache<PolicyDTO, Guid>(x => x.PolicyId);
        private HashSet<string> AllowedApplications { get; }
        private Dictionary<Guid, ApplicationDTO> _applications = new Dictionary<Guid, ApplicationDTO>();
        private Dictionary<Guid, PolicyUserDTO> _policyUsers = new Dictionary<Guid, PolicyUserDTO>();
        private Dictionary<Guid, RoleDTO> _roles = new Dictionary<Guid, RoleDTO>();

        public FilteredPoliciesRM(IConfiguredConnection conn, List<string> policyFilter = null)
           : base(nameof(FilteredPoliciesRM), () => conn.GetListener(nameof(FilteredPoliciesRM)))
        {
            if (policyFilter != null)
            {
                AllowedApplications = new HashSet<string>(policyFilter);
            }

            //set handlers
            EventStream.Subscribe<ApplicationMsgs.ApplicationCreated>(this);
            EventStream.Subscribe<ApplicationMsgs.PolicyCreated>(this);
            EventStream.Subscribe<ApplicationMsgs.RoleCreated>(this);
            EventStream.Subscribe<PolicyUserMsgs.PolicyUserAdded>(this);
            EventStream.Subscribe<PolicyUserMsgs.RoleAdded>(this);
            EventStream.Subscribe<PolicyUserMsgs.RoleRemoved>(this);
            EventStream.Subscribe<PolicyUserMsgs.UserDeactivated>(this);
            EventStream.Subscribe<PolicyUserMsgs.UserReactivated>(this);

            //read
            long ? checkpoint;
            using (var reader = conn.GetReader(nameof(FilteredPoliciesRM), this))
            {
                reader.EventStream.Subscribe<Message>(this);
                reader.Read<Domain.SecuredApplication>();
                reader.Read<Domain.PolicyUser>();
                checkpoint = reader.Position;
            }
            //subscribe
            Start<Domain.SecuredApplication>(checkpoint);
            Start<Domain.PolicyUser>(checkpoint);

            
        }


        public void Handle(ApplicationMsgs.ApplicationCreated @event)
        {
            if (AllowedApplications == null || //no filter
                AllowedApplications.Contains(@event.Name, StringComparer.OrdinalIgnoreCase)) //in filtered list
            {
                _applications.Add(@event.ApplicationId, new ApplicationDTO(@event));
                return;
            }
            //not in filtered list, ignore it
        }

        public void Handle(ApplicationMsgs.PolicyCreated @event)
        {
            if (_applications.ContainsKey(@event.ApplicationId)) //in filtered list
            {
                _polices.AddOrUpdate(new PolicyDTO(@event));
            }
        }

        public void Handle(ApplicationMsgs.RoleCreated @event)
        {
            var policy = _polices.Lookup(@event.PolicyId);
            if (policy.HasValue)
            {
                var role = new RoleDTO(@event);
                _roles.Add(@event.RoleId, role);
                policy.Value.Roles.AddOrUpdate(role);
            }
        }

        public void Handle(PolicyUserMsgs.PolicyUserAdded @event)
        {
            var policy = _polices.Lookup(@event.PolicyId);
            if (policy.HasValue)
            {
                var policyUser = new PolicyUserDTO(@event);
                _policyUsers.Add(@event.PolicyUserId, policyUser);
                policy.Value.Users.AddOrUpdate(policyUser);
            }
        }

        public void Handle(PolicyUserMsgs.RoleAdded @event)
        {
            if (_policyUsers.TryGetValue(@event.PolicyUserId, out var user) &&
                _roles.TryGetValue(@event.RoleId, out var role))
            {
                user.AddRole(role);
            }
        }

        public void Handle(PolicyUserMsgs.RoleRemoved @event)
        {
            if (_policyUsers.TryGetValue(@event.PolicyUserId, out var user))
            {
                user.RemoveRole(@event.RoleId);
            }
        }
        public void Handle(PolicyUserMsgs.UserDeactivated @event)
        {

            if (_policyUsers.TryGetValue(@event.PolicyUserId, out var user)){
                var policy = _polices.Lookup(user.PolicyId);
                if (policy.HasValue) {
                    policy.Value.Users.Remove(user);
                }
            }
        }

        public void Handle(PolicyUserMsgs.UserReactivated @event)
        {
            if (_policyUsers.TryGetValue(@event.PolicyUserId, out var user))
            {
                var policy = _polices.Lookup(user.PolicyId);
                if (policy.HasValue)
                {
                    policy.Value.Users.AddOrUpdate(user);
                }
            }
        }
    }
}
