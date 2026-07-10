using System.DirectoryServices.AccountManagement;
using System.Runtime.Versioning;

namespace ReactiveDomain.IdentityStorage.Services;
#if NETCOREAPP
[SupportedOSPlatform("windows")]
#endif
public static class ActiveDirectoryUserSearch {
	public static List<Principal> FindUserPrincipal(string userName, PrincipalContext principalContext) {
		List<Principal> results = [];
		using var searcher = new PrincipalSearcher(new UserPrincipal(principalContext));
		//PrincipalSearcher for machine for local users is case-sensitive, this is a bug with .net. So we have below workaround for local user search.
		//For more details on this refer to https://github.com/dotnet/corefx/issues/26779
		if (principalContext.ContextType == ContextType.Machine) {
			// Remove the leading & trailing * 
			userName = userName.Trim('*');
			var users = searcher.FindAll().Where(x => x.SamAccountName.Contains(userName, StringComparison.InvariantCultureIgnoreCase));
			results.AddRange(users);
		} else {
			//Create a "user object" in the context and specify the search parameters
			using var userPrincipalToSearch = new UserPrincipal(principalContext);
			userPrincipalToSearch.SamAccountName = userName;
			searcher.QueryFilter = userPrincipalToSearch;
			//Perform the search         
			results.AddRange(searcher.FindAll().Cast<UserPrincipal>().Where(p => p.EmployeeId != null).OrderBy(p => p.SamAccountName));
		}

		return results;
	}
	public static bool TryFindUserSid(string userName, string domain, out string userSid) {
		userSid = string.Empty;
		try {
			var contextType = domain.Equals(Environment.MachineName, StringComparison.CurrentCultureIgnoreCase) ? ContextType.Machine : ContextType.Domain;
			using var principalContext = new PrincipalContext(contextType, domain);
			var userPrincipal = FindUserPrincipal(userName, principalContext)
				.Cast<UserPrincipal>()
				.FirstOrDefault(p => p.SamAccountName.Length == userName.Length);
			if (userPrincipal != null) {
				userSid = userPrincipal.Sid.ToString();
				return true;
			}
		} catch (Exception) {
			// swallow the exception
		}
		return false;
	}
}
