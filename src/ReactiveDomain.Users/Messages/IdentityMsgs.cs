using ReactiveDomain.Messaging;

namespace Elbe.Messages
{
    /// <summary>
    /// Messages produced by the injected authentication module.
    /// </summary>
    public class IdentityMsgs
    {
        /// <summary>
        /// A user has been successfully authenticated in the system.
        /// </summary>
        public class UserAuthenticated : Event
        {
            /// <summary>The authentication provider.</summary>
            public readonly string AuthProvider;
            /// <summary>The domain in which the username exists.</summary>
            public readonly string AuthDomain;
            /// <summary>The username of the authenticated user.</summary>
            public readonly string UserName;
            /// <summary>The unique ID from the auth provider (e.g. Sub claim) of the authenticated user.</summary>
            public readonly string SubjectId;
            /// <summary>The IP address of the host asking for authentication.</summary>
            public readonly string HostIPAddress;

            /// <summary>
            /// A user has been successfully authenticated in the system.
            /// </summary>
            /// <param name="authProvider">The authentication provider.</param>
            /// <param name="authDomain">The domain in which the username exists.</param>
            /// <param name="userName">The username of the authenticated user.</param>
            /// <param name="subjectId">The unique ID from the auth provider (e.g. SID) of the authenticated user.</param>
            /// <param name="hostIPAddress">The IP address of the host asking for authentication.</param>
            public UserAuthenticated(
                string authProvider,
                string authDomain,
                string userName,
                string subjectId,
                string hostIPAddress)
            {
                UserName = userName;
                AuthProvider = authProvider;
                AuthDomain = authDomain;
                SubjectId = subjectId;
                HostIPAddress = hostIPAddress;
            }
        }
        /// <summary>
        /// A user has not been successfully authenticated in the system.
        /// </summary>
        public class UserAuthenticationFailed : Event
        {
            /// <summary>The authentication provider.</summary>
            public readonly string AuthProvider;
            /// <summary>The domain in which the username exists.</summary>
            public readonly string AuthDomain;
            /// <summary>The username of the not authenticated user.</summary>
            public readonly string UserName;
            /// <summary>The error why user could not be authenticated.</summary>
            public readonly string Error;
            /// <summary>The IP address of the host asking for authentication.</summary>
            public readonly string HostIPAddress;

            /// <summary>
            /// A user has not been successfully authenticated in the system.
            /// </summary>
            /// <param name="authProvider">The authentication provider.</param>
            /// <param name="authDomain">The domain in which the username exists.</param>
            /// <param name="userName">The username of the authenticated user.</param>
            /// <param name="error">The error why user could not be authenticated.</param>
            /// <param name="hostIPAddress">The IP address of the host asking for authentication.</param>
            public UserAuthenticationFailed(
                string authProvider,
                string authDomain,
                string userName,
                string error,
                string hostIPAddress)
            {
                UserName = userName;
                AuthProvider = authProvider;
                AuthDomain = authDomain;
                Error = error;
                HostIPAddress = hostIPAddress;
            }
        }
        /// <summary>
        /// A user has not been successfully authenticated in the system because user account is locked.
        /// </summary>
        public class UserAuthenticationFailedAccountLocked : Event
        {
            /// <summary>The authentication provider.</summary>
            public readonly string AuthProvider;
            /// <summary>The domain in which the username exists.</summary>
            public readonly string AuthDomain;
            /// <summary>The username of the authenticated user.</summary>
            public readonly string UserName;
            /// <summary>The IP address of the host asking for authentication.</summary>
            public readonly string HostIPAddress;

            /// <summary>
            /// A user has not been successfully authenticated in the system because user account is locked.
            /// </summary>
            /// <param name="authProvider">The authentication provider.</param>
            /// <param name="authDomain">The domain in which the username exists.</param>
            /// <param name="userName">The username of the not authenticated user.</param>
            /// <param name="hostIPAddress">The IP address of the host asking for authentication.</param> 
            public UserAuthenticationFailedAccountLocked(
                string authProvider,
                string authDomain,
                string userName,
                string hostIPAddress
                )
            {
                UserName = userName;
                AuthProvider = authProvider;
                AuthDomain = authDomain;
                HostIPAddress = hostIPAddress;
            }
        }
        /// <summary>
        /// A user has not been successfully authenticated in the system because user account is disabled.
        /// </summary>
        public class UserAuthenticationFailedAccountDisabled : Event
        {
            /// <summary>The authentication provider.</summary>
            public readonly string AuthProvider;
            /// <summary>The domain in which the username exists.</summary>
            public readonly string AuthDomain;
            /// <summary>The username of the authenticated user.</summary>
            public readonly string UserName;
            /// <summary>The IP address of the host asking for authentication.</summary>
            public readonly string HostIPAddress;

            /// <summary>
            /// A user has not been successfully authenticated in the system because user account is disabled.
            /// </summary>
            /// <param name="authProvider">The authentication provider.</param>
            /// <param name="authDomain">The domain in which the username exists.</param>
            /// <param name="userName">The username of the not authenticated user.</param>
            /// <param name="hostIPAddress">The IP address of the host asking for authentication.</param> 
            public UserAuthenticationFailedAccountDisabled(
                string authProvider,
                string authDomain,
                string userName,
                string hostIPAddress
                )
            {
                UserName = userName;
                AuthProvider = authProvider;
                AuthDomain = authDomain;
                HostIPAddress = hostIPAddress;
            }
        }
        /// <summary>
        /// A user has not been successfully authenticated in the system because invalid credentials were supplied.
        /// </summary>
        public class UserAuthenticationFailedInvalidCredentials : Event
        {
            /// <summary>The authentication provider.</summary>
            public readonly string AuthProvider;
            /// <summary>The domain in which the username exists.</summary>
            public readonly string AuthDomain;
            /// <summary>The username of the authenticated user.</summary>
            public readonly string UserName;
            /// <summary>The IP address of the host asking for authentication.</summary>
            public readonly string HostIPAddress;

            /// <summary>
            /// A user has not been successfully authenticated in the system because invalid credentials were supplied.
            /// </summary>
            /// <param name="authProvider">The authentication provider.</param>
            /// <param name="authDomain">The domain in which the username exists.</param>
            /// <param name="userName">The username of the not authenticated user.</param>
            /// <param name="hostIPAddress">The IP address of the host asking for authentication.</param> 
            public UserAuthenticationFailedInvalidCredentials(
                string authProvider,
                string authDomain,
                string userName,
                string hostIPAddress
                )
            {
                UserName = userName;
                AuthProvider = authProvider;
                AuthDomain = authDomain;
                HostIPAddress = hostIPAddress;
            }
        }
        /// <summary>
        /// A user has not been successfully authenticated in the system by external provider.
        /// </summary>
        public class UserAuthenticationFailedByExternalProvider : Event
        {
            /// <summary>The authentication provider.</summary>
            public readonly string AuthProvider;
            /// <summary>The IP address of the host asking for authentication.</summary>
            public readonly string HostIPAddress;

            /// <summary>
            /// A user has not been successfully authenticated in the system by an external provider.
            /// </summary>
            /// <param name="authProvider">The authentication provider.</param>
            /// <param name="hostIPAddress">The IP address of the host asking for authentication.</param> 
            public UserAuthenticationFailedByExternalProvider(
                string authProvider,
                string hostIPAddress
                )
            {
                AuthProvider = authProvider;
                HostIPAddress = hostIPAddress;
            }
        }
    }

}
