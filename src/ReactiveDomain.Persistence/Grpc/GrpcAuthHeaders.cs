namespace ReactiveDomain.Grpc;

/// <summary>gRPC metadata header names used by client auth interceptors.</summary>
public static class GrpcAuthHeaders {
	/// <summary>gRPC metadata key for the authorization header.</summary>
	public const string Authorization = "authorization";

	/// <summary>Scheme token for HTTP Basic auth (<c>Basic</c>).</summary>
	public const string BasicScheme = "Basic";
}
