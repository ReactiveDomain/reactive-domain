using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using ReactiveDomain.Foundation.Domain;
using ReactiveDomain.Policy.Messages;
using ReactiveDomain.Util;
[assembly: InternalsVisibleTo("ReactiveDomain.Identity")]
namespace ReactiveDomain.Policy.Domain
{

    internal class SecurityPolicy : ChildEntity
    {
        private readonly Dictionary<Guid, string> _rolesById = new Dictionary<Guid, string>();
        private readonly Dictionary<string, Guid> _rolesByName = new Dictionary<string, Guid>();
        public IReadOnlyList<Guid> Roles => _rolesById.Keys.ToList();
       
        public string ClientId { get; }
        public Guid AppId => base.Id;


        public SecurityPolicy(
            Guid policyId,
            string clientId,
            SecuredApplication root)
            : base(policyId, root)
        {
            //n.b. this method is called only inside an apply handler in the root aggregate
            // so setting values is ok, but raising events is not
            // the create event is raised in the root aggregate
            Register<RoleMsgs.RoleCreated>(Apply);
            ClientId = clientId;
        }

        //Apply State only if it applies to my id
        private void Apply(RoleMsgs.RoleCreated @event)
        {
            if (@event.PolicyId == Id)
            {
                _rolesById.Add(@event.RoleId, @event.Name);
                _rolesByName.Add(@event.Name, @event.RoleId);
            }
        }



        //Public methods
        /// <summary>
        /// Add a new role.
        /// </summary>
        public void AddRole(
            Guid roleId,
            string roleName) {
            if (roleId == Guid.Empty) {
                roleId = Guid.NewGuid();
            }
            if(string.IsNullOrWhiteSpace(roleName)) { throw new ArgumentNullException($"{nameof(roleId)} cannot be null, empty, or whitespace.");}
            
            if (_rolesById.ContainsValue(roleName) || _rolesById.ContainsKey(roleId))
            {
                throw new InvalidOperationException($"Cannot add duplicate role. RoleName: {roleName} RoleId:{roleId}");
            }

            Raise(new RoleMsgs.RoleCreated(
                roleId,
                roleName,
                Id));
        }

        public void GrantRole(PolicyUser user, string roleName)
        {
            Ensure.NotNull(user, nameof(user));
            Ensure.NotNullOrEmpty(roleName, nameof(roleName));
            Ensure.Equal(Id, user.PolicyId, nameof(user));
            if (!_rolesByName.TryGetValue(roleName, out var roleId)) {
                throw new ArgumentOutOfRangeException($"Policy {ClientId} does not contain Role {roleName}");
            }
            user.AddRole(roleName, roleId);
        }
        public void RemoveRole(PolicyUser user, string roleName)
        {
            Ensure.NotNull(user, nameof(user));
            Ensure.NotNullOrEmpty(roleName, nameof(roleName));
            Ensure.Equal(Id, user.PolicyId, nameof(user));
            if (!_rolesByName.TryGetValue(roleName, out var roleId)) {
                throw new ArgumentOutOfRangeException($"Policy {ClientId} does not contain Role {roleName}");
            }
            user.RemoveRole(roleName, roleId);
        }
    }

}
