using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Threading.Tasks;
using ReactiveDomain.IdentityStorage.Domain;

using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Policy.ReadModels;
using ReactiveDomain.Util;

namespace ReactiveDomain.Authentication
{
    public static class UserAuthorization
    {
        //public static async Task<bool> IsUserAuthorized(IDispatcher bus, IdentityModel.OidcClient.LoginResult loginResult, IUserModelCollection usersCollection, IEnumerable<string> allowedRoles)
        //{
        //    bool isAuthorized = false;
        //    bool isFirstTimeExternalProviderAuthentication = false;
        //    UserModel user = null;
        //    var provider = loginResult.User.Claims.FirstOrDefault(x => x.Type == "idp")?.Value;
        //    var results = await LoginService.GetUserInfo(loginResult.AccessToken);
        //    var claimsValues = LoginService.GetClaimsValues(results.Claims);
        //    user = usersCollection.UsersModelList.FirstOrDefault(x =>
        //        x.AuthDomain.Equals(claimsValues.AuthDomain, StringComparison.CurrentCultureIgnoreCase)
        //        && (string.IsNullOrEmpty(x.SubjectId) ||
        //           string.IsNullOrEmpty(claimsValues.SubjectId)
        //            ? x.UserName.Equals(claimsValues.UserName, StringComparison.CurrentCultureIgnoreCase)
        //            : x.SubjectId.Equals(claimsValues.SubjectId, StringComparison.CurrentCultureIgnoreCase))
        //        && x.IsActivated);

        //    if (user == null && !string.Equals(provider, ConfigurationManager.AppSettings["PKIStsName"] ?? "PKIStsServer"))
        //    {
        //        //case external provider first time Authenticated
        //        user = usersCollection.UsersModelList.FirstOrDefault(x => x.AuthDomain.Equals(claimsValues.AuthDomain, StringComparison.CurrentCultureIgnoreCase)
        //                                                && x.UserName.Equals(claimsValues.Email, StringComparison.CurrentCultureIgnoreCase)
        //                                                && x.IsActivated);
        //        isFirstTimeExternalProviderAuthentication = true;
        //    }
        //    var roles = allowedRoles.ToList();
        //    //Update user info 
        //    if (user != null)
        //    {
        //        UpdateUserInfo(user, claimsValues, provider, bus);
        //        //In case of external provider authenticated user, we don't get the UserSidFromAuthProvider until after we authenticate the user for the first time.
        //        // and hence we will get the role claims only after we have called UpdateUserInfo to actually update
        //        // user's info based on what we get from authentication provider. 
        //        // This is because when we add external provider users to ES, we don't have their UserSidFromAuthProvider.
        //        if (isFirstTimeExternalProviderAuthentication)
        //        {
        //            results = await LoginService.GetUserInfo(loginResult.AccessToken);
        //        }

        //        foreach (var role in roles)
        //        {
        //            if (LoginService.IsRolePresentInClaims(results.Claims, role))
        //            {
        //                isAuthorized = true;
        //                break;
        //            }
        //        }
        //    }
        //    // let's check the roles assigned in ES not just on the claims
        //    if (user != null && !isAuthorized)
        //    {
        //        if (user.Roles.Items.Any(r => roles.Contains(r.Name) && r.IsSelected))
        //        {
        //            isAuthorized = true;
        //        }
        //    }

        //    return isAuthorized;
        //}

        // Todo: delete the above IsUserAuthorized method once this is ready
        public static async Task<bool> IsUserAuthorized(User user, FilteredPoliciesRM policyRM)
        {
            Ensure.NotNull(user, nameof(User));
            bool isAuthorized = false;
            bool isFirstTimeExternalProviderAuthentication = false;

            //todo: extract and call this from bootstrap
            /*
            var results = await UserValidation.GetUserInfo(loginResult.AccessToken);
            var claimsValues = UserValidation.GetClaimsValues(results.Claims);

            user = policyRM.ActivatedUsers.FirstOrDefault(
                x => x.AuthDomain.Equals(claimsValues.AuthDomain, StringComparison.CurrentCultureIgnoreCase) &&
                     (string.IsNullOrEmpty(x.SubjectId) ||
                      string.IsNullOrEmpty(claimsValues.SubjectId)
                          ? x.UserName.Equals(claimsValues.UserName, StringComparison.CurrentCultureIgnoreCase)
                          : x.SubjectId.Equals(claimsValues.SubjectId, StringComparison.CurrentCultureIgnoreCase)));
            */
            //todo: also extract and call this from bootstrap
            /*
           var provider = user.Principal.Claims.FirstOrDefault(x => x.Type == "idp")?.Value;
           if (user == null && !string.Equals(provider, Constants.PKISTSName))
           {
               //case external provider first time Authenticated
               user = policyRM.ActivatedUsers.FirstOrDefault(x => x.AuthDomain.Equals(claimsValues.AuthDomain, StringComparison.CurrentCultureIgnoreCase)
                                                                         && x.UserName.Equals(claimsValues.Email, StringComparison.CurrentCultureIgnoreCase)
                                                                         && x.IsActivated);
               isFirstTimeExternalProviderAuthentication = true;
           }
           */

            /*
            if (user != null)
            {
                //Todo: this commented out for the moment until we agree on the best way to update the user details 
                //UpdateUserInfo(user, claimsValues, provider, bus);

                //In case of external provider authenticated user, we don't get the UserSidFromAuthProvider until after we authenticate the user for the first time.
                // and hence we will get the role claims only after we have called UpdateUserInfo to actually update
                // user's info based on what we get from authentication provider. 
                // This is because when we add external provider users to ES, we don't have their UserSidFromAuthProvider.
                if (isFirstTimeExternalProviderAuthentication)
                {
                    results = await UserValidation.GetUserInfo(loginResult.AccessToken);
                }

               
            }
            */
            //todo: hash sets?
            //todo: also reconfirm logic to make sure we are checking the right thing here
            //foreach (var role in user.IdentityRoles)
            //{
            //    //todo: ensure this is string equality by making Role ICompariable
            //    if (user.Roles.Contains(role))
            //    {
            //        isAuthorized = true;
            //        break;
            //    }
            //}
            return isAuthorized;
        }

        //private static void UpdateUserInfo(UserModel user, ClaimsModel claimsValues, string provider, IDispatcher bus)
        //{
        //    if (!string.IsNullOrEmpty(claimsValues.GivenName) && !string.Equals(claimsValues.GivenName, user.GivenName))
        //    {
        //        ////Send update givenname command 
        //        //bus.TrySend(
        //        //    MessageBuilder.New(
        //        //        () => new UserMsgs.UpdateGivenName(
        //        //                user.UserId,
        //        //                claimsValues.GivenName)),
        //        //    out _);
        //    }
        //    if (!string.IsNullOrEmpty(claimsValues.Surname) && !string.Equals(claimsValues.Surname, user.Surname))
        //    {
        //        ////Send update surname command 
        //        //bus.TrySend(
        //        //    MessageBuilder.New(
        //        //        () => new UserMsgs.UpdateSurname(
        //        //                user.UserId,
        //        //                claimsValues.Surname)),
        //        //    out _);
        //    }
        //    if (!string.IsNullOrEmpty(claimsValues.FullName) && !string.Equals(claimsValues.FullName, user.FullName))
        //    {
        //        ////Send update fullname command 
        //        //bus.TrySend(
        //        //    MessageBuilder.New(
        //        //        () => new UserMsgs.UpdateFullName(
        //        //                user.UserId,
        //        //                claimsValues.FullName)),
        //        //    out _);
        //    }
        //    if (!string.IsNullOrEmpty(claimsValues.Email) && !string.Equals(claimsValues.Email, user.Email))
        //    {
        //        ////Send update email command 
        //        //bus.TrySend(
        //        //    MessageBuilder.New(
        //        //        () => new UserMsgs.UpdateEmail(
        //        //                user.UserId,
        //        //                claimsValues.Email)),
        //        //    out _);
        //    }
        //    if (!string.IsNullOrEmpty(claimsValues.UserName) && !string.Equals(claimsValues.UserName, user.UserName))
        //    {
        //        //Send update username command 
        //        //bus.TrySend(
        //        //    MessageBuilder.New(
        //        //        () => new UserMsgs.UpdateUserName(
        //        //                user.UserId,
        //        //                claimsValues.UserName)),
        //        //    out _);
        //    }
        //    //if (string.IsNullOrEmpty(user.UserSidFromAuthProvider))
        //    //{
        //    //    // Send update UserSidFromAuthProvider command 
        //    //    bus.TrySend(
        //    //        MessageBuilder.New(
        //    //            () => new UserMsgs.UpdateUserSidFromAuthProvider(
        //    //                    user.UserId,
        //    //                    claimsValues.UserSidFromAuthProvider)),
        //    //        out _);
        //    //}
        //}
    }
}
