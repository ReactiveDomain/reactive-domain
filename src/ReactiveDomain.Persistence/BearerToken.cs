namespace ReactiveDomain;

/// <summary>
/// A bearer token used for connection-scoped <c>authorization</c> headers on gRPC stream-store calls.
/// </summary>
public sealed class BearerToken {
	/// <summary>The raw token value (without the <c>Bearer </c> scheme prefix).</summary>
	public string Value { get; }

	/// <summary>
	/// Constructs a new <see cref="BearerToken"/>.
	/// </summary>
	/// <param name="value">Non-empty bearer token.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is null.</exception>
	/// <exception cref="ArgumentException">Thrown when <paramref name="value"/> is empty.</exception>
	public BearerToken(string value) {
		ArgumentException.ThrowIfNullOrEmpty(value);
		Value = value;
	}

	/// <inheritdoc />
	public override string ToString() => Value;
}
