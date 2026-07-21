using System.Text;
using Grpc.Core;
using Grpc.Core.Interceptors;

namespace ReactiveDomain.Grpc;

/// <summary>
/// Client interceptor that stamps every call with an
/// <c>authorization: Basic base64(username:password)</c> header. Install via
/// <c>KurrentDBClientSettings.Interceptors</c>. This is the credential scheme real KurrentDB nodes accept.
/// </summary>
/// <remarks>
/// As with <see cref="BearerTokenInterceptor"/>, the header is stamped connection-wide rather than
/// passed as per-call KurrentDB <c>UserCredentials</c>, which the .NET client drops on insecure channels.
/// </remarks>
public sealed class BasicAuthInterceptor : Interceptor {
	private readonly string _headerValue;

	/// <summary>
	/// Builds an interceptor that stamps Basic auth for <paramref name="username"/> /
	/// <paramref name="password"/>.
	/// </summary>
	public BasicAuthInterceptor(string username, string password) {
		var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
		_headerValue = $"{GrpcAuthHeaders.BasicScheme} {encoded}";
	}

	private void AddAuthorization<TRequest, TResponse>(ref ClientInterceptorContext<TRequest, TResponse> context)
		where TRequest : class where TResponse : class {
		var headers = context.Options.Headers;
		if (headers is null) {
			headers = new global::Grpc.Core.Metadata();
		}
		if (headers.Get(GrpcAuthHeaders.Authorization) is null) {
			headers.Add(GrpcAuthHeaders.Authorization, _headerValue);
		}

		context = new ClientInterceptorContext<TRequest, TResponse>(
			context.Method, context.Host, context.Options.WithHeaders(headers));
	}

	/// <inheritdoc />
	public override TResponse BlockingUnaryCall<TRequest, TResponse>(TRequest request,
		ClientInterceptorContext<TRequest, TResponse> context,
		BlockingUnaryCallContinuation<TRequest, TResponse> continuation) {
		AddAuthorization(ref context);
		return continuation(request, context);
	}

	/// <inheritdoc />
	public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(TRequest request,
		ClientInterceptorContext<TRequest, TResponse> context,
		AsyncUnaryCallContinuation<TRequest, TResponse> continuation) {
		AddAuthorization(ref context);
		return continuation(request, context);
	}

	/// <inheritdoc />
	public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(
		ClientInterceptorContext<TRequest, TResponse> context,
		AsyncClientStreamingCallContinuation<TRequest, TResponse> continuation) {
		AddAuthorization(ref context);
		return continuation(context);
	}

	/// <inheritdoc />
	public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(TRequest request,
		ClientInterceptorContext<TRequest, TResponse> context,
		AsyncServerStreamingCallContinuation<TRequest, TResponse> continuation) {
		AddAuthorization(ref context);
		return continuation(request, context);
	}

	/// <inheritdoc />
	public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(
		ClientInterceptorContext<TRequest, TResponse> context,
		AsyncDuplexStreamingCallContinuation<TRequest, TResponse> continuation) {
		AddAuthorization(ref context);
		return continuation(context);
	}
}
