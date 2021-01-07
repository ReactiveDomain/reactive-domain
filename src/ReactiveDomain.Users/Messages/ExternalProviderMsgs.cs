using System;
using ReactiveDomain.Messaging;

namespace ReactiveDomain.Users.Messages
{
    public class ExternalProviderMsgs
    {
        public class ProviderCreated : Event
        {
            /// <summary>The unique ID of this identity provider</summary>
            public readonly Guid ProviderId;
            /// <summary>The name of this identity provider</summary>
            public readonly string ProviderName;

            /// <summary>
            /// A stream for an identity provider was created.
            /// </summary>
            public ProviderCreated(
                Guid providerId,
                string providerName)
            {
                ProviderId = providerId;
                ProviderName = providerName;
            }
        }

        /// <summary>
        /// A user was not successfully authenticated because invalid credentials were supplied.
        /// </summary>
        public class AuthenticationFailedInvalidCredentials : Event
        {
            /// <summary>The ID of the provider.</summary>
            public readonly Guid ProviderId;
            /// <summary>The date and time in UTC on the Elbe Server when the authentication was logged.</summary>
            public readonly DateTime TimeStamp;
            /// <summary>The IP address of the host asking for authentication.</summary>
            public readonly string HostIPAddress;

            /// <summary>
            /// A user was not successfully authenticated because invalid credentials were supplied.
            /// </summary>
            /// <param name="providerId">The ID of the not authenticated user.</param>
            /// <param name="timeStamp">The date and time in UTC on the Elbe Server when the authentication attempt was logged.</param>
            /// <param name="hostIPAddress">The IP address of the host asking for authentication.</param>
            public AuthenticationFailedInvalidCredentials(
                Guid providerId,
                DateTime timeStamp,
                string hostIPAddress)
            {
                ProviderId = providerId;
                TimeStamp = timeStamp;
                HostIPAddress = hostIPAddress;
            }
        }
    }
}
