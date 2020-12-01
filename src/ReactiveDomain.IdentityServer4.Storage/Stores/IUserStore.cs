using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using ReactiveDomain.Users.Identity;


namespace ReactiveDomain.IdentityServer4.Storage.Stores
{
    public interface IUserStore
    {
        CredentialValidationResult ValidateCredentials(string domain, string username, string password);

        Task<IdentityUser> FindBySubjectId(string subjectId);

        IdentityUser FindByUsername(string domain, string username);

        // Summary:
        //     Finds the user by external provider.
        //
        // Parameters:
        //   provider:
        //     The provider.
        //
        //   userId:
        //     The user identifier.
        IdentityUser FindByExternalProvider(string provider, string userId);

        int GetUserCount();
        IdentityUser AutoProvisionUser(string provider, string providerUserId, List<Claim> list);
        bool AddEventClaims(string subjectId, List<Claim> list);

    }
}
