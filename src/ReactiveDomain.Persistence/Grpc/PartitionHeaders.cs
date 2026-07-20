namespace ReactiveDomain.Grpc;

/// <summary>
/// gRPC metadata header that selects the storage partition for a request.
/// Absent → the host default partition. Set once per connection by <see cref="PartitionInterceptor"/>.
/// </summary>
public static class PartitionHeaders {
	/// <summary>Metadata key for the storage partition (<c>streamstore-partition</c>).</summary>
	public const string Partition = "streamstore-partition";
}
