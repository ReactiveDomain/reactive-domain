using Grpc.Core;
using Grpc.Core.Interceptors;

namespace ReactiveDomain.Grpc;

/// <summary>
/// Client interceptor that stamps every call with a fixed <c>streamstore-partition</c> header.
/// One connection = one partition; install via <c>KurrentDBClientSettings.Interceptors</c>.
/// </summary>
public sealed class PartitionInterceptor(string partition) : Interceptor {
	private void AddPartition<TRequest, TResponse>(ref ClientInterceptorContext<TRequest, TResponse> context)
		where TRequest : class where TResponse : class {
		var headers = context.Options.Headers;
		if (headers is null) {
			headers = new global::Grpc.Core.Metadata();
		}
		if (headers.Get(PartitionHeaders.Partition) is null) {
			headers.Add(PartitionHeaders.Partition, partition);
		}

		context = new ClientInterceptorContext<TRequest, TResponse>(
			context.Method, context.Host, context.Options.WithHeaders(headers));
	}

	/// <inheritdoc />
	public override TResponse BlockingUnaryCall<TRequest, TResponse>(TRequest request,
		ClientInterceptorContext<TRequest, TResponse> context,
		BlockingUnaryCallContinuation<TRequest, TResponse> continuation) {
		AddPartition(ref context);
		return continuation(request, context);
	}

	/// <inheritdoc />
	public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(TRequest request,
		ClientInterceptorContext<TRequest, TResponse> context,
		AsyncUnaryCallContinuation<TRequest, TResponse> continuation) {
		AddPartition(ref context);
		return continuation(request, context);
	}

	/// <inheritdoc />
	public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(
		ClientInterceptorContext<TRequest, TResponse> context,
		AsyncClientStreamingCallContinuation<TRequest, TResponse> continuation) {
		AddPartition(ref context);
		return continuation(context);
	}

	/// <inheritdoc />
	public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(TRequest request,
		ClientInterceptorContext<TRequest, TResponse> context,
		AsyncServerStreamingCallContinuation<TRequest, TResponse> continuation) {
		AddPartition(ref context);
		return continuation(request, context);
	}

	/// <inheritdoc />
	public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(
		ClientInterceptorContext<TRequest, TResponse> context,
		AsyncDuplexStreamingCallContinuation<TRequest, TResponse> continuation) {
		AddPartition(ref context);
		return continuation(context);
	}
}
