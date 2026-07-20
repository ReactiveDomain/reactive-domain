using System.Text;
using Grpc.Core;
using Grpc.Core.Interceptors;
using ReactiveDomain.Grpc;
using Xunit;

namespace ReactiveDomain.Testing.Tests.StreamStore;

/// <summary>
/// Pins connection-wide auth/partition interceptors (headers must not rely on per-call credentials).
/// <c>global::Grpc.Core.Metadata</c> is fully qualified — ReactiveDomain also defines a Metadata type.
/// </summary>
public sealed class GrpcClientInterceptorTests {
	private static Method<string, string> UnaryMethod() =>
		new(MethodType.Unary, "svc", "m",
			Marshallers.Create(_ => Array.Empty<byte>(), _ => string.Empty),
			Marshallers.Create(_ => Array.Empty<byte>(), _ => string.Empty));

	[Fact]
	public void bearer_stamps_authorization_header() {
		var interceptor = new BearerTokenInterceptor("secret-token");
		global::Grpc.Core.Metadata? captured = null;
		var context = new ClientInterceptorContext<string, string>(UnaryMethod(), host: null, new CallOptions());

		interceptor.BlockingUnaryCall("request", context, (_, ctx) => {
			captured = ctx.Options.Headers;
			return "response";
		});

		Assert.NotNull(captured);
		var auth = captured!.Get(GrpcAuthHeaders.Authorization);
		Assert.NotNull(auth);
		Assert.Equal("Bearer secret-token", auth!.Value);
	}

	[Fact]
	public void bearer_does_not_overwrite_existing_authorization() {
		var interceptor = new BearerTokenInterceptor("new-token");
		var preset = new global::Grpc.Core.Metadata { { GrpcAuthHeaders.Authorization, "Bearer preset" } };
		global::Grpc.Core.Metadata? captured = null;
		var context = new ClientInterceptorContext<string, string>(UnaryMethod(), host: null,
			new CallOptions(headers: preset));

		interceptor.BlockingUnaryCall("request", context, (_, ctx) => {
			captured = ctx.Options.Headers;
			return "response";
		});

		Assert.Equal("Bearer preset", captured!.Get(GrpcAuthHeaders.Authorization)!.Value);
	}

	[Fact]
	public void basic_stamps_base64_authorization_header() {
		var interceptor = new BasicAuthInterceptor("admin", "changeit");
		global::Grpc.Core.Metadata? captured = null;
		var context = new ClientInterceptorContext<string, string>(UnaryMethod(), host: null, new CallOptions());

		interceptor.BlockingUnaryCall("request", context, (_, ctx) => {
			captured = ctx.Options.Headers;
			return "response";
		});

		var expected = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("admin:changeit"));
		Assert.Equal(expected, captured!.Get(GrpcAuthHeaders.Authorization)!.Value);
	}

	[Fact]
	public void partition_stamps_streamstore_partition_header() {
		var interceptor = new PartitionInterceptor("b_testdb");
		global::Grpc.Core.Metadata? captured = null;
		var context = new ClientInterceptorContext<string, string>(UnaryMethod(), host: null, new CallOptions());

		interceptor.BlockingUnaryCall("request", context, (_, ctx) => {
			captured = ctx.Options.Headers;
			return "response";
		});

		Assert.Equal("b_testdb", captured!.Get(PartitionHeaders.Partition)!.Value);
	}
}
