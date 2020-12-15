using IdentityModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ReactiveDomain.IdentityServer4.Storage.Stores;
using ReactiveDomain.Users.Identity;
using IdentityServer4.Services;
using IdentityServer4.Models;
using IdentityServer4.Extensions;

namespace ReactiveDomain.IdentityServer4.Storage.Services
{
    public class UserProfileService : IProfileService
    {

        protected readonly IUserStore _userStore;
        //protected readonly IUserEntitlementRM _userEntitlementRM;

        public UserProfileService(IUserStore userRepository /*, IUserEntitlementRM userEntitlementRM*/)
        {
            _userStore = userRepository;
            //_userEntitlementRM = userEntitlementRM;
        }

        public async Task GetProfileDataAsync(ProfileDataRequestContext context)
        {
            //Logger.LogDebug("Get profile called for subject {subject} from client {client} with claim types {claimTypes} via {caller}",
            //    context.Subject.GetSubjectId(),
            //    context.Client.ClientName ?? context.Client.ClientId,
            //    context.RequestedClaimTypes,
            //    context.Caller);
            var sub = context.Subject.FindFirst("sub").Value;
            if (sub != null)
            {
                var user = await _userStore.FindBySubjectId(sub);

                var claimsPrincipal = await GetClaimsAsync(context.Client.ClientId, user);
                var claims = claimsPrincipal.Claims.ToList();
                if (context.RequestedClaimTypes != null && context.RequestedClaimTypes.Any())
                {
                    claims = claims.Where(x => context.RequestedClaimTypes.Contains(x.Type)).ToList();
                    context.IssuedClaims = claims;
                }

            }
        }
        private async Task<ClaimsPrincipal> GetClaimsAsync(string clientId, SubjectDTO user)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }
            return await Task.Factory.StartNew(() =>
            {
                var claimsIdentity = new ClaimsIdentity();
                List<Claim> claims = GetClaimsForUser(clientId, user);
                if (claims != null)
                {
                    claimsIdentity.AddClaims(claims);
                }
                return new ClaimsPrincipal(claimsIdentity);
            });
        }

        private List<Claim> GetClaimsForUser(string clientId, SubjectDTO user)
        {
            var claims = new List<Claim>();
            try
            {
                throw new NotImplementedException("todo");
                //var roles = _userEntitlementRM.RolesForUser(user.SubjectId, user.Username, user.DomainName, clientId);
                //if (roles.Count > 0)
                //{
                //    roles.ForEach(role => claims.Add(new Claim(JwtClaimTypes.Role, role.Name)));
                //}
            }
            catch (UserDeactivatedException udEx)
            {
                //Logger.LogInformation(udEx.Message);
            }
            catch (UserNotFoundException unEx)
            {
                //Logger.LogInformation(unEx.Message);
            }
            // Add other claims to the list
            if (user.Roles != null)
            {
                claims.AddRange(user.Roles);
            }
            return claims;
        }

        public async Task IsActiveAsync(IsActiveContext context)
        {
            bool isUserActive = context.IsActive;
            if (_userStore != null && _userStore.GetUserCount() > 0)
            {
                var user = await _userStore.FindBySubjectId(context.Subject.GetSubjectId());
                isUserActive = user != null;
            }
            context.IsActive = isUserActive;
        }
    }
}