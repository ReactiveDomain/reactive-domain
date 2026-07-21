using Grpc.Core;
using Grpc.Core.Interceptors;

namespace ReactiveDomain.Grpc;

/// <summary>
/// Client interceptor that stamps every call with an <c>authorization: Bearer &lt;token&gt;</c> header.
/// Install via <c>KurrentDBClientSettings.Interceptors</c> (one connection = one token).
/// </summary>
/// <remarks>
/// Auth must ride a connection-wide interceptor rather than per-call KurrentDB <c>UserCredentials</c>:
/// the .NET gRPC client drops per-call credentials on insecure (h2c / <c>tls=false</c>) channels.
/// </remarks>
public sealed class BearerTokenInterceptor(string token) : Interceptor {
	private readonly string _headerValue = "Bearer " + token;

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
