using System;


namespace ReactiveDomain
{
    /// <summary>
    /// A username/password pair used for authentication and
    /// authorization to perform operations over an <see cref="T:EventStore.ClientAPI.IEventStoreConnection" />.
    /// </summary>
    public class UserCredentials
    {
        /// <summary>The username</summary>
        public readonly string Username;
        /// <summary>The password</summary>
        public readonly string Password;

        /// <summary>
        /// Constructs a new <see cref="T:EventStore.ClientAPI.SystemData.UserCredentials" />.
        /// </summary>
        /// <param name="username">
        /// </param>
        /// <param name="password">
        /// </param>
        public UserCredentials(string username, string password) {
            if (string.IsNullOrWhiteSpace(username)) throw new ArgumentNullException(nameof(username));
            if (string.IsNullOrWhiteSpace(password)) throw new ArgumentNullException(nameof(password));
            Username = username;
            Password = password;
        }
    }
}
