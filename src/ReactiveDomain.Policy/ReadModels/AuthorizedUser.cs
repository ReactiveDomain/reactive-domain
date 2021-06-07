using System;
using System.Collections.Generic;
using System.Security.Claims;

namespace ReactiveDomain.Policy.ReadModels
{
    public class AuthorizedUser
    {
        public AuthorizedUser(
            ClaimsPrincipal user,
            string accessToken,
            string identityToken,
            string refreshToken,
            DateTime accessTokenExpiration,
            DateTime authenticationTime)
        {
            User = user;
            AccessToken = accessToken;
            IdentityToken = identityToken;
            RefreshToken = refreshToken;
            AccessTokenExpiration = accessTokenExpiration;
            AuthenticationTime = authenticationTime;
        }
        /// <summary>
        /// Gets or sets the user.
        /// </summary>
        /// <value>
        /// The user.
        /// </value>
        public ClaimsPrincipal User { get; }

        /// <summary>
        /// Gets or sets the access token.
        /// </summary>
        /// <value>
        /// The access token.
        /// </value>
        public string AccessToken { get; internal set; }

        /// <summary>
        /// Gets or sets the identity token.
        /// </summary>
        /// <value>
        /// The identity token.
        /// </value>
        public string IdentityToken { get; }

        /// <summary>
        /// Gets or sets the refresh token.
        /// </summary>
        /// <value>
        /// The refresh token.
        /// </value>
        public string RefreshToken { get; internal set; }

        /// <summary>
        /// Gets or sets the access token expiration.
        /// </summary>
        /// <value>
        /// The access token expiration.
        /// </value>
        public DateTime AccessTokenExpiration { get; internal set; }

        /// <summary>
        /// Gets or sets the authentication time.
        /// </summary>
        /// <value>
        /// The authentication time.
        /// </value>
        public DateTime AuthenticationTime { get; internal set; }

        public HashSet<string> RoleSet { get; internal set; }
        public HashSet<Type> PermissionSet { get; internal set; }

    }
}
