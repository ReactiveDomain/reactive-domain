using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using IdentityModel;
using IdentityModel.OidcClient;
using IdentityModel.OidcClient.Browser;
using IdentityModel.OidcClient.Results;
using ReactiveDomain.Foundation;
using ReactiveDomain.IdentityStorage.ReadModels;
using ReactiveDomain.IdentityStorage.Services;
using ReactiveDomain.Policy;
using ReactiveDomain.Policy.ReadModels;

namespace ReactiveDomain.Authentication
{
    public class LoginService
    {
        private OidcClient _oidcClient;
        private IBrowser _browser;
        private LoginResult _loginResult;
        private readonly IConfiguredConnection _conn;
        private readonly string _policyName;
        private readonly Guid _policyId;
        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly string _tokenServerUrl;
        private readonly HashSet<Role> _roles;
        private readonly IdentityServer4.Models.Client _client;
        private readonly UsersRm _usersRm;
        private readonly ApplicationRm _appRm;

        public LoginService(
            IConfiguredConnection conn,
            string policyName,
            string clientId,
            string clientSecret,
            string tokenServerUrl,
            HashSet<Role> roles)
        {
            _conn = conn;
            _policyName = policyName;
            _clientId = clientId;
            _clientSecret = clientSecret;
            _tokenServerUrl = tokenServerUrl;
            _roles = roles;
            var clientStore = new ClientStore(_conn);
            _client = clientStore.FindClientByIdAsync(clientId).Result;
            _usersRm = new UsersRm(_conn);
            _appRm = new ApplicationRm(_conn);
            _policyId = _appRm.GetPolicies(policyName).First().PolicyId;
            //IdentityModelEventSource.ShowPII = true;
        }

        public async Task<LoginResponse> DisplayLoginUI()
        {
            _loginResult = new LoginResult("unauthorized");

            _browser ??= new WebViewWrapper();
            var options = new OidcClientOptions
            {
                Authority = _tokenServerUrl,
                ClientId = _clientId,
                Scope = "openid rd-policy",
                RedirectUri = _client.RedirectUris.First(),
                PostLogoutRedirectUri = _client.PostLogoutRedirectUris.First(),
                Flow = OidcClientOptions.AuthenticationFlow.AuthorizationCode,
                ClientSecret = _clientSecret,
                FilterClaims = false,
                Browser = _browser,
                ResponseMode = OidcClientOptions.AuthorizeResponseMode.FormPost,
                ClockSkew = TimeSpan.Zero,
                LoadProfile = false,
            };
            try
            {
                _oidcClient ??= new OidcClient(options);

                if (_loginResult == null || _loginResult.IsError)
                {
                    _loginResult = await _oidcClient.LoginAsync(new LoginRequest());
                }
            }
            catch
            {
                _loginResult = new LoginResult("unauthorized");
                //todo: confirm these are the correct result values
                return new LoginResponse(Result.LoginFailed, _loginResult, UserPolicy.EmptyPolicy());
            }
            ClaimsIdentity claimsIdentity = _loginResult?.User?.Identity as ClaimsIdentity;
            var authenticated = claimsIdentity?.IsAuthenticated ?? false;
            if (!authenticated)
            {
                return new LoginResponse(Result.LoginFailed, _loginResult, UserPolicy.EmptyPolicy());
            }
            if (!claimsIdentity.Claims.Any(c => c.Type == "policy-access" && c.Value.ToLower() == _policyName.ToLower()))
            {
                return new LoginResponse(Result.AccessDenied, _loginResult, UserPolicy.EmptyPolicy());
            }
            var userId = claimsIdentity.Claims.FirstOrDefault(c => c.Type == "rd-userid").Value;
            var polUser = _appRm.GetPolicyUserByuserId(Guid.Parse(userId));
            var user = _usersRm.UsersById[Guid.Parse(userId)];
            var grantedRoles = new HashSet<Role>();
            foreach (var role in _roles)
            {
                if (polUser.Roles.Any(r => r.Name == role.RoleName)) { grantedRoles.Add(role); }
            }
            return new LoginResponse(Result.Authorized, _loginResult, new UserPolicy(user, grantedRoles));
        }
        public async Task Logout()
        {
            try
            {
                if (_oidcClient != null)
                {
                    if (_loginResult != null && string.IsNullOrEmpty(_loginResult.IdentityToken) == false)
                    {
                        var logoutRequest = new LogoutRequest
                        {
                            IdTokenHint = _loginResult.IdentityToken
                        };
                        await _oidcClient.LogoutAsync(logoutRequest);
                        _oidcClient = null;
                        _loginResult = null;
                    }
                }
            }
            catch
            {
                _loginResult = null;
                throw;
            }

        }
        public bool IsRolePresentInClaims(IEnumerable<Claim> claims, string role)
        {
            if (string.IsNullOrEmpty(role)) return false;
            var roleClaimsValues = claims.Where(c => c.Type == JwtClaimTypes.Role).Select(c => c.Value);
            return roleClaimsValues.Contains(role);
        }

        public ClaimsModel GetClaimsValues(IEnumerable<Claim> claims)
        {
            var claimList = claims.ToList();
            var givenName = claimList.FirstOrDefault(x => x.Type == JwtClaimTypes.GivenName)?.Value;
            var surName = claimList.FirstOrDefault(x => x.Type == JwtClaimTypes.FamilyName)?.Value;
            var email = claimList.FirstOrDefault(x => x.Type == JwtClaimTypes.Email)?.Value;
            var subjectId = claimList.FirstOrDefault(x => x.Type == JwtClaimTypes.Subject)?.Value;
            var fullName = claimList.FirstOrDefault(x => x.Type == JwtClaimTypes.PreferredUserName)?.Value;
            // "name" claim will always be DomainName\UserName format.
            var name = claimList.FirstOrDefault(x => x.Type == JwtClaimTypes.Name)?.Value ?? string.Empty;

            return new ClaimsModel(
                subjectId,
                 givenName,
                 surName,
                 email,
                 fullName,
                 name.Split('\\').Length == 2 ? name.Split('\\')[0] : string.Empty,
                 name.Split('\\').Length == 2 ? name.Split('\\')[1] : string.Empty);
        }
        public async Task<UserInfoResult> GetUserInfo(string accessToken)
        {
            UserInfoResult userInfoResult = await _oidcClient.GetUserInfoAsync(accessToken);
            return userInfoResult;
        }
    }
}
