using System;
using ReactiveDomain.Messaging;

namespace ReactiveDomain.Identity.Messages
{
    public class SubjectMsgs {
        /// <summary>
        /// A new user login tracker was created.
        /// </summary>
        public class SubjectCreated : Event {
            /// <summary>The unique Id of the login idenity subject.</summary>
            public readonly Guid SubjectId;

            /// <summary>The unique Id of the user.</summary>
            public readonly Guid UserId;

            /// <summary>The unique ID from the auth provider (e.g. Sub Claim) of the authenticated user.</summary>
            public readonly string SubClaim;

            /// <summary>The identity provider.</summary>
            public readonly string AuthProvider;

            /// <summary>The user's domain.</summary>
            public readonly string AuthDomain;

            /// <summary>
            /// A new user was created.
            /// </summary>
            /// <param name="subjectId">The unique ID of the new user.</param>
            ///<param name="subClaim">The unique ID from the auth provider (e.g. Sub Claim) of the authenticated user.</param>
            /// <param name="authProvider">The identity provider.</param>
            /// <param name="authDomain">The user's domain.</param>
            public SubjectCreated(
                Guid subjectId,
                Guid userId,
                string subClaim,
                string authProvider,
                string authDomain) {
                SubjectId = subjectId;
                UserId = userId;
                SubClaim = subClaim;
                AuthProvider = authProvider;
                AuthDomain = authDomain;
            }
        }

        /// <summary>
        /// A user was successfully authenticated.
        /// </summary>
        public class Authenticated : Event {
            /// <summary>The ID of the authenticated user.</summary>
            public readonly Guid SubjectId;

            /// <summary>The date and time in UTC on the Elbe Server when the authentication was logged.</summary>
            public readonly DateTime TimeStamp;

            /// <summary>The IP address of the host asking for authentication.</summary>
            public readonly string HostIpAddress;

            /// <summary>
            /// A user was successfully authenticated.
            /// </summary>
            /// <param name="subjectId">The ID of the authenticated user.</param>
            /// <param name="timeStamp">The date and time in UTC on the Elbe Server when the authentication was logged.</param>
            /// <param name="hostIpAddress">The IP address of the host asking for authentication.</param>
            public Authenticated(
                Guid subjectId,
                DateTime timeStamp,
                string hostIpAddress) {
                SubjectId = subjectId;
                TimeStamp = timeStamp;
                HostIpAddress = hostIpAddress;
            }
        }

        /// <summary>
        /// A user was not successfully authenticated because account is locked.
        /// </summary>
        public class AuthenticationFailedAccountLocked : Event {
            /// <summary>The ID of the not authenticated user.</summary>
            public readonly Guid SubjectId;

            /// <summary>The date and time in UTC on the Elbe Server when the authentication was logged.</summary>
            public readonly DateTime TimeStamp;

            /// <summary>The IP address of the host asking for authentication.</summary>
            public readonly string HostIpAddress;

            /// <summary>
            /// A user was not successfully authenticated because account is locked.
            /// </summary>
            /// <param name="subjectId">The ID of the not authenticated user.</param>
            /// <param name="timeStamp">The date and time in UTC on the Elbe Server when the authentication attempt was logged.</param>
            /// <param name="hostIpAddress">The IP address of the host asking for authentication.</param>
            public AuthenticationFailedAccountLocked(
                Guid subjectId,
                DateTime timeStamp,
                string hostIpAddress) {
                SubjectId = subjectId;
                TimeStamp = timeStamp;
                HostIpAddress = hostIpAddress;
            }
        }

        /// <summary>
        /// A user was not successfully authenticated because account is disabled.
        /// </summary>
        public class AuthenticationFailedAccountDisabled : Event {
            /// <summary>The ID of the not authenticated user.</summary>
            public readonly Guid SubjectId;

            /// <summary>The date and time in UTC on the Elbe Server when the authentication was logged.</summary>
            public readonly DateTime TimeStamp;

            /// <summary>The IP address of the host asking for authentication.</summary>
            public readonly string HostIpAddress;

            /// <summary>
            /// A user was not successfully authenticated because account is disabled.
            /// </summary>
            /// <param name="subjectId">The ID of the not authenticated user.</param>
            /// <param name="timeStamp">The date and time in UTC on the Elbe Server when the authentication attempt was logged.</param>
            /// <param name="hostIpAddress">The IP address of the host asking for authentication.</param>
            public AuthenticationFailedAccountDisabled(
                Guid subjectId,
                DateTime timeStamp,
                string hostIpAddress) {
                SubjectId = subjectId;
                TimeStamp = timeStamp;
                HostIpAddress = hostIpAddress;
            }
        }

        /// <summary>
        /// A user was not successfully authenticated because invalid credentials were supplied.
        /// </summary>
        public class AuthenticationFailedInvalidCredentials : Event {
            /// <summary>The ID of the not authenticated user.</summary>
            public readonly Guid SubjectId;

            /// <summary>The date and time in UTC on the Elbe Server when the authentication was logged.</summary>
            public readonly DateTime TimeStamp;

            /// <summary>The IP address of the host asking for authentication.</summary>
            public readonly string HostIpAddress;

            /// <summary>
            /// A user was not successfully authenticated because invalid credentials were supplied.
            /// </summary>
            /// <param name="subjectId">The ID of the not authenticated user.</param>
            /// <param name="timeStamp">The date and time in UTC on the Elbe Server when the authentication attempt was logged.</param>
            /// <param name="hostIpAddress">The IP address of the host asking for authentication.</param>
            public AuthenticationFailedInvalidCredentials(
                Guid subjectId,
                DateTime timeStamp,
                string hostIpAddress) {
                SubjectId = subjectId;
                TimeStamp = timeStamp;
                HostIpAddress = hostIpAddress;
            }
        }

    }
}
