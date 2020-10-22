using System;
using System.Collections.Generic;
using System.DirectoryServices.AccountManagement;
using System.Linq;

namespace ReactiveDomain.Identity.Storage
{
    public static class ActiveDirectoryUserSearch
    {
        public static List<Principal> FindUserPrincipal(string userName, PrincipalContext principalContext)
        {
            List<Principal> results = new List<Principal>();
            using (var searcher = new PrincipalSearcher(new UserPrincipal(principalContext)))
            {
                //PrincipalSearcher for machine for local users is case sensitive, this is a bug with .net. So we have below workaround for local user search.
                //For more details on this refer to https://github.com/dotnet/corefx/issues/26779
                if (principalContext.ContextType == ContextType.Machine)
                {
                    // Remove the leading & trailing * (this comes when called from Elbe.WPF)
                    userName = userName.Trim('*');
                    var users = searcher.FindAll().Where(x =>
                    {
                        // .net 4.8 doesn't have case insensitive version of Contains(), but dotnet core does. Since Elbe is targeted for both frameworks, I had the use IndexOf to achieve
                        // a case insensitive behavior. Other option would be to write an extension method, which would be doing the same thing (calling IndexOf).
                        return x.SamAccountName.IndexOf(userName, StringComparison.InvariantCultureIgnoreCase) >= 0;
                    });
                    results.AddRange(users);
                }
                else
                {
                    //Create a "user object" in the context and specify the search parameters
                    using (var userPrincipalToSearch = new UserPrincipal(principalContext))
                    {
                        userPrincipalToSearch.SamAccountName = userName;
                        searcher.QueryFilter = userPrincipalToSearch;
                        //Perform the search         
                        results.AddRange(searcher.FindAll().Cast<UserPrincipal>().Where(p => p.EmployeeId != null).OrderBy(p => p.SamAccountName));
                    }
                }
            }
            return results;
        }
        public static bool TryFindUserSid(string userName, string domain, out string userSid)
        {
            userSid = string.Empty;
            try
            {
                var contextType = domain.Equals(Environment.MachineName, StringComparison.CurrentCultureIgnoreCase) ? ContextType.Machine : ContextType.Domain;
                using (var principalContext = new PrincipalContext(contextType, domain))
                {
                    var userPrincipal = FindUserPrincipal(userName, principalContext)
                        .Cast<UserPrincipal>()
                        .FirstOrDefault(p=> p.SamAccountName.Length == userName.Length);
                    if (userPrincipal != null)
                    {
                        userSid = userPrincipal.Sid.ToString();
                        return true;
                    }
                }
            }
            catch (Exception)
            {
            }
            return false;
        }
    }
}
