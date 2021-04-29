using ReactiveDomain.Messaging;
using System;

namespace ReactiveDomain.Users.Messages
{
    public class SubjectMsgs
    {
        /// <summary>
        /// A new user login tracker was created.
        /// </summary>
        public class SubjectCreated : Event
        {
            /// <summary>The unique Id of the tracked user.</summary>
            public readonly Guid SubjectId;
            /// <summary>The unique ID from the auth provider (e.g. Sub Claim) of the authenticated user.</summary>
            public readonly string SubClaim;
            /// <summary>The identity provider.</summary>
            public readonly string AuthProvider;
            /// <summary>The user's domain.</summary>
            public readonly string AuthDomain;
            /// <summary>
            /// A new user was created.
            /// </summary>
            /// <param name="id">The unique ID of the new user.</param>
            ///<param name="subjectId">The unique ID from the auth provider (e.g. Sub Claim) of the authenticated user.</param>
            /// <param name="authProvider">The identity provider.</param>
            /// <param name="authDomain">The user's domain.</param>
            public SubjectCreated(
                Guid subjectId,
                string subClaim,
                string authProvider,
                string authDomain)
            {
                SubjectId = subjectId;
                SubClaim = subClaim;
                AuthProvider = authProvider;
                AuthDomain = authDomain;
            }
        }
        /// <summary>
        /// A user was successfully authenticated.
        /// </summary>
        public class Authenticated : Event
        {
            /// <summary>The ID of the authenticated user.</summary>
            public readonly Guid SubjectId;
            /// <summary>The date and time in UTC on the Elbe Server when the authentication was logged.</summary>
            public readonly DateTime TimeStamp;
            /// <summary>The IP address of the host asking for authentication.</summary>
            public readonly string HostIPAddress;

            /// <summary>
            /// A user was successfully authenticated.
            /// </summary>
            /// <param name="id">The ID of the authenticated user.</param>
            /// <param name="timeStamp">The date and time in UTC on the Elbe Server when the authentication was logged.</param>
            /// <param name="hostIPAddress">The IP address of the host asking for authentication.</param>
            public Authenticated(
                Guid subjectId,
                DateTime timeStamp,
                string hostIPAddress)
            {
                SubjectId = subjectId;
                TimeStamp = timeStamp;
                HostIPAddress = hostIPAddress;
            }
        }
        /// <summary>
        /// A user was not successfully authenticated.
        /// </summary>
        public class AuthenticationFailed : Event
        {
            /// <summary>The ID of the not authenticated user.</summary>
            public readonly Guid SubjectId;
            /// <summary>The date and time in UTC on the Elbe Server when the authentication was logged.</summary>
            public readonly DateTime TimeStamp;
            /// <summary>The IP address of the host asking for authentication.</summary>
            public readonly string HostIPAddress;

            /// <summary>
            /// A user was successfully authenticated.
            /// </summary>
            /// <param name="id">The ID of the not authenticated user.</param>
            /// <param name="timeStamp">The date and time in UTC on the Elbe Server when the authentication attempt was logged.</param>
            /// <param name="hostIPAddress">The IP address of the host asking for authentication.</param>
            public AuthenticationFailed(
                Guid subjectId,
                DateTime timeStamp,
                string hostIPAddress)
            {
                SubjectId = subjectId;
                TimeStamp = timeStamp;
                HostIPAddress = hostIPAddress;
            }
        }
        /// <summary>
        /// A user was not successfully authenticated because account is locked.
        /// </summary>
        public class AuthenticationFailedAccountLocked : Event
        {
            /// <summary>The ID of the not authenticated user.</summary>
            public readonly Guid SubjectId;
            /// <summary>The date and time in UTC on the Elbe Server when the authentication was logged.</summary>
            public readonly DateTime TimeStamp;
            /// <summary>The IP address of the host asking for authentication.</summary>
            public readonly string HostIPAddress;

            /// <summary>
            /// A user was not successfully authenticated because account is locked.
            /// </summary>
            /// <param name="id">The ID of the not authenticated user.</param>
            /// <param name="timeStamp">The date and time in UTC on the Elbe Server when the authentication attempt was logged.</param>
            /// <param name="hostIPAddress">The IP address of the host asking for authentication.</param>
            public AuthenticationFailedAccountLocked(
                Guid subjectId,
                DateTime timeStamp,
                string hostIPAddress)
            {
                SubjectId = subjectId;
                TimeStamp = timeStamp;
                HostIPAddress = hostIPAddress;
            }
        }

        /// <summary>
        /// A user was not successfully authenticated because account is disabled.
        /// </summary>
        public class AuthenticationFailedAccountDisabled : Event
        {
            /// <summary>The ID of the not authenticated user.</summary>
            public readonly Guid SubjectId;
            /// <summary>The date and time in UTC on the Elbe Server when the authentication was logged.</summary>
            public readonly DateTime TimeStamp;
            /// <summary>The IP address of the host asking for authentication.</summary>
            public readonly string HostIPAddress;

            /// <summary>
            /// A user was not successfully authenticated because account is disabled.
            /// </summary>
            /// <param name="id">The ID of the not authenticated user.</param>
            /// <param name="timeStamp">The date and time in UTC on the Elbe Server when the authentication attempt was logged.</param>
            /// <param name="hostIPAddress">The IP address of the host asking for authentication.</param>
            public AuthenticationFailedAccountDisabled(
                Guid subjectId,
                DateTime timeStamp,
                string hostIPAddress)
            {
                SubjectId = subjectId;
                TimeStamp = timeStamp;
                HostIPAddress = hostIPAddress;
            }
        }

        /// <summary>
        /// A user was not successfully authenticated because invalid credentials were supplied.
        /// </summary>
        public class AuthenticationFailedInvalidCredentials : Event
        {
            /// <summary>The ID of the not authenticated user.</summary>
            public readonly Guid SubjectId;
            /// <summary>The date and time in UTC on the Elbe Server when the authentication was logged.</summary>
            public readonly DateTime TimeStamp;
            /// <summary>The IP address of the host asking for authentication.</summary>
            public readonly string HostIPAddress;

            /// <summary>
            /// A user was not successfully authenticated because invalid credentials were supplied.
            /// </summary>
            /// <param name="id">The ID of the not authenticated user.</param>
            /// <param name="timeStamp">The date and time in UTC on the Elbe Server when the authentication attempt was logged.</param>
            /// <param name="hostIPAddress">The IP address of the host asking for authentication.</param>
            public AuthenticationFailedInvalidCredentials(
                Guid subjectId,
                DateTime timeStamp,
                string hostIPAddress)
            {
                SubjectId = subjectId;
                TimeStamp = timeStamp;
                HostIPAddress = hostIPAddress;
            }
        }

        public class ClientGrantAdded : Event
        {
            private Guid Id;
            private Guid ApplicationId;
            private Guid PolicyId;


            public ClientGrantAdded(Guid id, Guid applicationId, Guid policyId)
            {
                Id = id;
                ApplicationId = applicationId;
                PolicyId = policyId;
            }
        }

        internal class ClientGrantRemoved : Event
        {
            private Guid Id;
            private Guid ApplicationId;
            private Guid PolicyId;

            public ClientGrantRemoved(Guid id, Guid applicationId, Guid policyId)
            {
                Id = id;
                ApplicationId = applicationId;
                PolicyId = policyId;
            }
        }

        public class ClientAccessGranted : Event
        {
            public Guid Id;
            public DateTime Timestamp;
            public string ClientAudience;

            public ClientAccessGranted(Guid id, DateTime timestamp, string clientAudience)
            {
                Id = id;
                Timestamp = timestamp;
                ClientAudience = clientAudience;
            }
        }
        public class ClientAccessDenied : Event
        {
            public Guid Id;
            public DateTime Timestamp;
            public string ClientAudience;

            public ClientAccessDenied(Guid id, DateTime timestamp, string clientAudience)
            {
                Id = id;
                Timestamp = timestamp;
                ClientAudience = clientAudience;
            }
        }
    }
}
