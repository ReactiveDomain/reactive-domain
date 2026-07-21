using ReactiveDomain.EventStore;
using Xunit;

namespace ReactiveDomain.Testing.Tests.StreamStore;

/// <summary>
/// Pins constructor auth validation and per-call credential rejection for
/// <see cref="GrpcConnectionWrapper"/>.
/// </summary>
public sealed class GrpcConnectionWrapperAuthTests {
	private static readonly Uri TestUri = new("esdb://127.0.0.1:2113?tls=false");

	[Fact]
	public void primary_ctor_allows_null_credentials() {
		using var conn = new GrpcConnectionWrapper("test", TestUri, credentials: null);
		Assert.Equal("test", conn.ConnectionName);
	}

	[Fact]
	public void primary_ctor_accepts_user_credentials() {
		using var conn = new GrpcConnectionWrapper(
			"test", TestUri, new UserCredentials("admin", "changeit"));
		Assert.Equal("test", conn.ConnectionName);
	}

	[Fact]
	public void bearer_ctor_rejects_null_token() {
		Assert.Throws<ArgumentNullException>(() =>
			new GrpcConnectionWrapper("test", TestUri, bearerToken: null!));
	}

	[Fact]
	public void bearer_token_rejects_empty_value() {
		var ex = Assert.Throws<ArgumentException>(() => new BearerToken(""));
		Assert.Equal("value", ex.ParamName);
	}

	[Fact]
	public void bearer_token_rejects_null_value() {
		Assert.Throws<ArgumentNullException>(() => new BearerToken(null!));
	}

	[Fact]
	public void user_credentials_rejects_null_username() {
		Assert.Throws<ArgumentNullException>(() => new UserCredentials(null!, "changeit"));
	}

	[Fact]
	public void user_credentials_rejects_null_password() {
		Assert.Throws<ArgumentNullException>(() => new UserCredentials("admin", null!));
	}

	[Fact]
	public void reject_per_call_credentials_throws_when_non_null() {
		Assert.Throws<NotSupportedException>(() =>
			GrpcConnectionWrapper.RejectPerCallCredentials(new UserCredentials("u", "p")));
	}

	[Fact]
	public void reject_per_call_credentials_allows_null() {
		GrpcConnectionWrapper.RejectPerCallCredentials(null);
	}

	[Fact]
	public void append_rejects_per_call_credentials_without_calling_store() {
		using var conn = new GrpcConnectionWrapper("test", TestUri);
		Assert.Throws<NotSupportedException>(() =>
			conn.AppendToStream("s", ExpectedVersion.Any, new UserCredentials("u", "p")));
	}

	[Fact]
	public void append_rejects_per_call_credentials_even_when_connection_has_credentials() {
		using var conn = new GrpcConnectionWrapper(
			"test", TestUri, new UserCredentials("admin", "changeit"));
		Assert.Throws<NotSupportedException>(() =>
			conn.AppendToStream("s", ExpectedVersion.Any, new UserCredentials("admin", "changeit")));
	}
}
