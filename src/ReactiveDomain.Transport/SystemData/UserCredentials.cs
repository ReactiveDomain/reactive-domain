using ReactiveDomain.Util;

namespace ReactiveDomain.Transport.SystemData;

/// <summary>
/// A username/password pair used to authenticate and authorize operations over a connection.
/// </summary>
public class UserCredentials {
	/// <summary>
	/// The username
	/// </summary>
	public readonly string Username;
	/// <summary>
	/// The password
	/// </summary>
	public readonly string Password;

	/// <summary>
	/// Constructs a new <see cref="UserCredentials"/>.
	/// </summary>
	/// <param name="username"></param>
	/// <param name="password"></param>
	public UserCredentials(string username, string password) {
		Ensure.NotNull(username, "username");
		Ensure.NotNull(password, "password");

		Username = username;
		Password = password;
	}
}
