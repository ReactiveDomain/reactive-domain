using IdentityModel;
using IdentityServerHost.Quickstart.UI;
using ReactiveDomain.Users.Identity;
using System;
using System.Collections.Generic;
using System.DirectoryServices.AccountManagement;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ReactiveDomain.IdentityServer4.Storage.Stores
{
    public class UserStore : IUserStore
    {
        
        readonly List<SubjectDTO> _users = new List<SubjectDTO>();

        
        public CredentialValidationResult ValidateCredentials(string domainName, string userName, string password)
        {
            CredentialValidationResult credentialValidationResult = new CredentialValidationResult
            {
                Result = false,
                ErrorDescription = string.Empty
            };

            //Logger.LogInformation($"ValidateCredentials called for user {(!string.IsNullOrEmpty(domainName) ? $"{domainName}\\{userName}" : userName)}");

            var contextType = string.IsNullOrEmpty(domainName) || domainName.Equals(Environment.MachineName, StringComparison.CurrentCultureIgnoreCase) ? ContextType.Machine : ContextType.Domain;

            try
            {
                var principalContext = new PrincipalContext(contextType, domainName);
                using (principalContext)
                {
                    try
                    {
                        var userPrincipal = ActiveDirectoryUserSearch.FindUserPrincipal(userName, principalContext).FirstOrDefault() as UserPrincipal;
                        if (userPrincipal != null)
                        {
                            ContextOptions options = ContextOptions.Negotiate;
                            bool authSucceeded = principalContext.ValidateCredentials(userName, password, options);
                            if (authSucceeded)
                            {
                                SubjectDTO newUser = GetPKIStsUserFromUserPrincipal(domainName, userPrincipal);
                                if (newUser != null)
                                {
                                    // refresh our list with the user we just got.
                                    _users.RemoveAll(u => u.SubjectId == newUser.SubjectId);
                                    _users.Add(newUser);
                                }
                                credentialValidationResult.Result = true;
                            }
                            else
                            {
                                if (userPrincipal.IsAccountLockedOut())
                                {
                                    credentialValidationResult.ErrorDescription = AccountOptions.UserAccountIsLockedErrorMessage;
                                }
                                if (userPrincipal.Enabled == false)
                                {
                                    credentialValidationResult.ErrorDescription = AccountOptions.UserAccountIsDisabledErrorMessage;
                                }

                            }
                        }
                        else
                        {
                            credentialValidationResult.ErrorDescription = AccountOptions.InvalidCredentialsErrorMessage;
                        }
                    }
                    catch (Exception e)
                    {
                        credentialValidationResult.ErrorDescription = e.Message;
                    }

                }
            }
            catch (Exception e)
            {
               // Logger.LogError($"{e.Message}, stack trace of the exception is:{e.StackTrace}");
                credentialValidationResult.ErrorDescription = e.Message;
            }

            return credentialValidationResult;
        }

        private SubjectDTO GetPKIStsUserFromUserPrincipal(string domainName, UserPrincipal user)
        {
            string userName = domainName != null ? $"{domainName}\\{user.SamAccountName}" : user.SamAccountName;
            string firstName = user.GivenName;
            string lastName = user.Surname;
            string displayName = user.DisplayName;
            string email = user.EmailAddress;

            var pkiStsUser = new SubjectDTO
            {
                SubjectId = user.Sid.ToString(),
                Username = user.SamAccountName,
                DomainName = domainName,
                ProviderName = user.Context.Name,
                ProviderSubjectId = user.Context.ConnectedServer,
                Claims = new List<Claim>()
            };
            pkiStsUser.Roles.Add(new Claim(JwtClaimTypes.Name, userName));
            pkiStsUser.Roles.Add(new Claim(JwtClaimTypes.GivenName, firstName ?? string.Empty));
            pkiStsUser.Roles.Add(new Claim(JwtClaimTypes.FamilyName, lastName ?? string.Empty));
            pkiStsUser.Roles.Add(new Claim(JwtClaimTypes.PreferredUserName, displayName ?? string.Empty));
            pkiStsUser.Roles.Add(new Claim(JwtClaimTypes.Email, email ?? string.Empty));
            return pkiStsUser;
        }

        public int GetUserCount()
        {
            return _users.Count;
        }
        public async Task<SubjectDTO> FindBySubjectId(string subjectId)
        {
            return await Task.Factory.StartNew(() =>
            {
                return _users.FirstOrDefault(x => x.SubjectId == subjectId);
            });
        }
        public SubjectDTO FindByUsername(string domain, string userName)
        {
            var contextType = string.IsNullOrEmpty(domain) || domain.Equals(Environment.MachineName, StringComparison.CurrentCultureIgnoreCase) ? ContextType.Machine : ContextType.Domain;
            SubjectDTO user = FindUserBySamAccountName(userName, domain, contextType);
            return user;
        }
        private SubjectDTO FindUserBySamAccountName(string userName, string domainName, ContextType contextType)
        {
            SubjectDTO user = null;
            using (var context = new PrincipalContext(contextType))
            {
                var userPrincipal = ActiveDirectoryUserSearch.FindUserPrincipal(userName, context).FirstOrDefault() as UserPrincipal;
                if (userPrincipal != null)
                {
                    user = GetPKIStsUserFromUserPrincipal(domainName, userPrincipal);
                }
            }
            return user;
        }

        public SubjectDTO FindByExternalProvider(string provider, string userId)
        {
            return _users.FirstOrDefault(x => x.ProviderName == provider && x.SubjectId == userId);
        }

        public SubjectDTO AutoProvisionUser(string provider, string providerUserId, List<Claim> claims)
        {
            var nameClaim = claims.FirstOrDefault(c => c.Type == JwtClaimTypes.Name);
            var userName = nameClaim == null ? string.Empty : provider != null ? $"{provider}\\{nameClaim.Value}" : nameClaim.Value;
            var emailClaim = claims.FirstOrDefault(c => c.Type == JwtClaimTypes.Email);
            var userEmail = emailClaim == null ? string.Empty : emailClaim.Value;
            var user = new SubjectDTO
            {
                Username = userEmail,
                DomainName = provider,
                ProviderName = provider,
                ProviderSubjectId = provider,
                SubjectId = providerUserId,
                Claims = claims
            };
            if (nameClaim != null)
            {
                user.Roles[user.Roles.IndexOf(nameClaim)] = new Claim(JwtClaimTypes.Name, userName);
            }
            var existingUser = _users.FirstOrDefault(x => x.SubjectId == user.SubjectId);
            if (existingUser != null)
            {
                _users.Remove(existingUser);
            }
            _users.Add(user);
            return user;
        }

        public bool AddEventClaims(string subjectId, List<Claim> listEventClaims)
        {
            bool bRet = false;
            var existingUser = _users.FirstOrDefault(x => x.SubjectId == subjectId);
            if (existingUser != null)
            {
                bRet = true;
                existingUser.Claims.AddRange(listEventClaims);
            }
            return bRet;
        }
    }
}
