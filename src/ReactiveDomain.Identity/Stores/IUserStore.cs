using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using ReactiveDomain.Users.Identity;


namespace ReactiveDomain.IdentityServer4.Storage.Stores
{
    public interface IUserStore
    {
        CredentialValidationResult ValidateCredentials(string domain, string username, string password);

        Task<SubjectDTO> FindBySubjectId(string subjectId);

        SubjectDTO FindByUsername(string domain, string username);

        // Summary:
        //     Finds the user by external provider.
        //
        // Parameters:
        //   provider:
        //     The provider.
        //
        //   userId:
        //     The user identifier.
        SubjectDTO FindByExternalProvider(string provider, string userId);

        int GetUserCount();
        SubjectDTO AutoProvisionUser(string provider, string providerUserId, List<Claim> list);
        bool AddEventClaims(string subjectId, List<Claim> list);

    }
}
