using System;
using System.Collections.Generic;
using System.DirectoryServices.AccountManagement;
using System.Linq;
using System.Security.Claims;
using IdentityModel;

namespace ReactiveDomain.IdentityStorage.Services
{
    public class ValidationResult
    {
        public bool IsValidated;
        public string Sub;
        public string DisplayName;
        public ClaimsPrincipal Identity;
        public string ErrorMessage;
    }

    public class UserValidation
    {
        private readonly UserStore _userStore;

        public UserValidation(UserStore userStore)
        {
            _userStore = userStore;
        }
        public bool TryFindUserPrincipal(string domainName, string userName, out UserPrincipal userPrincipal)
        {

            if (string.IsNullOrEmpty(domainName))
            {
                domainName = Environment.MachineName;
            }
            var contextType =
                domainName.Equals(Environment.MachineName, StringComparison.CurrentCultureIgnoreCase)
                    ? ContextType.Machine
                    : ContextType.Domain;
            try
            {
                var principalContext = new PrincipalContext(contextType, domainName);


                //look up the user in Active Directory or the Local Machine
                if (ActiveDirectoryUserSearch.FindUserPrincipal(userName, principalContext)
                    .FirstOrDefault() is UserPrincipal user)
                {
                    userPrincipal = user;
                    return true;
                }
            }
            catch
            {
                //ignore
            }
            userPrincipal = null;
            return false;
        }
        public ValidationResult Validate(string domainName, string userName, string password, string clientId, string remoteHttpAddress)
        {
            var result = new ValidationResult();

            //Log.Information(
            //    $"ValidateCredentials called for user {(!string.IsNullOrEmpty(domainName) ? $"{domainName}\\{userName}" : userName)}");
            if (string.IsNullOrEmpty(domainName))
            {
                domainName = Environment.MachineName;
            }
            var contextType =
                domainName.Equals(Environment.MachineName, StringComparison.CurrentCultureIgnoreCase)
                    ? ContextType.Machine
                    : ContextType.Domain;

            try
            {
                var principalContext = new PrincipalContext(contextType, domainName);

                try
                {
                    //look up the user in Active Directory or the Local Machine
                    if (ActiveDirectoryUserSearch.FindUserPrincipal(userName, principalContext)
                        .FirstOrDefault() is UserPrincipal user)
                    {
                        result.Sub = user.Sid.Value;
                        result.DisplayName = user.DisplayName;
                        //Use the UserStore to record the process as the user is authenticated
                        var userId = _userStore.UpdateUserInfo(user, domainName, contextType.ToString());
                        var options = ContextOptions.Negotiate;
                        bool authSucceeded = principalContext.ValidateCredentials(userName, password, options);

                        if (authSucceeded)
                        {
                            //build the Claims Principal to return in the token
                            result.IsValidated = true;
                            _userStore.UserAuthenticated(user, domainName, contextType.ToString(), remoteHttpAddress, clientId);
                            var additionalClaims = _userStore.GetAdditionalClaims(userId);
                            result.Identity = BuildClaimsPrincipal(user, additionalClaims);
                        }
                        else
                        {
                            if (user.IsAccountLockedOut())
                            {
                                result.ErrorMessage = "Account Locked";
                                _userStore.UserAccountLocked(user, domainName, contextType.ToString(), remoteHttpAddress, clientId);
                            }

                            if (user.Enabled == false)
                            {
                                result.ErrorMessage = "Account Disabled";
                                _userStore.UserAccountDisabled(user, domainName, contextType.ToString(), remoteHttpAddress, clientId);
                            }
                            //bad password
                            result.ErrorMessage = "Invalid password";
                            _userStore.UserProvidedInvalidCredentials(user, domainName, contextType.ToString(), remoteHttpAddress, clientId);

                        }
                    }
                    else
                    {
                        //bad username, no user to log this against
                        result.ErrorMessage = "Invalid username";
                        //todo: log attempts with bad user names, currently only in the local log: needs a bit of design
                        //note this will only trigger if AD can't find the user name
                        //either have a single stream for all unknown names or add streams for bad names likely option 1
                    }
                }
                catch (Exception e)
                {
                    result.ErrorMessage = e.Message;
                }

            }
            catch (Exception e)
            {
                //Log.Error($"{e.Message}, stack trace of the exception is:{e.StackTrace}");
                result.ErrorMessage = e.Message;
            }

            return result;
        }

        private ClaimsPrincipal BuildClaimsPrincipal(UserPrincipal user, List<Claim> additionalClaims)
        {
            var claims = new List<Claim> { new Claim(JwtClaimTypes.Subject, user.Sid.Value) };
            if (additionalClaims?.Any() ?? false) { claims.AddRange(additionalClaims); }
            var id = new ClaimsIdentity(
                claims.Distinct(new ClaimComparer()), "IdentityServer4", JwtClaimTypes.Name, JwtClaimTypes.Role);
            return new ClaimsPrincipal(id);
        }
    }
}
