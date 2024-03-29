﻿using DynamicData;
using ReactiveDomain.Policy.Messages;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ReactiveDomain.Policy.ReadModels
{
    public class PolicyUserDTO
    {
        public Guid PolicyUserId { get; }
        public Guid UserId { get; }
        public Guid PolicyId { get; }
        public bool OneRolePerUser { get; }
        public IObservableCache<RoleDTO, Guid> RolesCache;
        private ISourceCache<RoleDTO, Guid> _rolesSource = new SourceCache<RoleDTO, Guid>(t => t.Id);       
        public HashSet<RoleDTO> Roles = new HashSet<RoleDTO>();
        public PolicyUserDTO(Guid policyUserId, Guid userId, Guid policyId, bool oneRolePerUser, List<RoleDTO> roles = null)
        {
            PolicyUserId = policyUserId;
            UserId = userId;
            PolicyId = policyId;
            OneRolePerUser = oneRolePerUser;
            RolesCache = _rolesSource.AsObservableCache();
            AddRoles(roles);
        }

        public PolicyUserDTO(PolicyUserMsgs.PolicyUserAdded @event)
        {
            PolicyUserId = @event.PolicyUserId;
            UserId = @event.UserId;
            PolicyId = @event.PolicyId;
            RolesCache = _rolesSource.AsObservableCache();
            OneRolePerUser = @event.OneRolePerUser;
        }
        public void AddRoles(List<RoleDTO> roles)
        {
            if (roles == null || !roles.Any()) return;
            if (OneRolePerUser)
            {                
                AddRole(roles.FirstOrDefault());
            }
            else {
                foreach (var role in roles)
                {
                    AddRole(role);
                }
            }
        }
        public void AddRole(RoleDTO role)
        {
            if (role == null) { return; }
            if (OneRolePerUser) {
                _rolesSource.Clear();
                Roles.Clear();
            }
            _rolesSource.AddOrUpdate(role);
            Roles.Add(role);
        }

        public void RemoveRole(Guid roleId)
        {
            _rolesSource.Remove(roleId);
            var role = Roles.First(x => x.Id == roleId);
            Roles.Remove(role);
        }
    }
}
