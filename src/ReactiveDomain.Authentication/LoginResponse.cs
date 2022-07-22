using IdentityModel.OidcClient;
using ReactiveDomain.Policy;

namespace ReactiveDomain.Authentication
{
    public class LoginResponse
    {
        public Result Result { get; }
        public UserPolicy ResolvedPolicy { get; }
        public LoginResult OdicLoginResult { get; }
        public LoginResponse(Result result, LoginResult odicResult, UserPolicy resolvedPolicy)
        {
            Result = result;
            OdicLoginResult = odicResult;
            ResolvedPolicy = resolvedPolicy;
        }
    }
    public enum Result
    {
        Authorized, // User Indentiy and accesss confirmed
        AccessDenied, // User Identity Confirm, Client Access denied
        LoginFailed // User identity not confirmed
    }
}
