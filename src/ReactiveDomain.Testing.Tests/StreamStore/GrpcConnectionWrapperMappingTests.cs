using KurrentDB.Client;
using ReactiveDomain.EventStore;
using Xunit;
using EsdbPosition = KurrentDB.Client.Position;
using EsdbStreamPosition = KurrentDB.Client.StreamPosition;

namespace ReactiveDomain.Testing.Tests.StreamStore;

/// <summary>
/// Pins <see cref="GrpcConnectionWrapper.ToRecordedEvent"/>: ProjectedEvent tagging,
/// CreatedEpoch Unix-ms, Position from OriginalPosition, and null-event skip.
/// </summary>
public sealed class GrpcConnectionWrapperMappingTests {
	private static Dictionary<string, string> Meta(string type, string contentType = "application/json") => new() {
		["type"] = type,
		["created"] = "1000",
		["content-type"] = contentType
	};

	private static EventRecord MakeEvent(string stream, ulong number, string type, Uuid? id = null,
		EsdbPosition? position = null) =>
		new(stream, id ?? Uuid.NewUuid(), new EsdbStreamPosition(number),
			position ?? new EsdbPosition(number, number),
			Meta(type), new byte[] { 1, 2, 3 }, new byte[] { 4, 5 });

	[Fact]
	public void link_resolved_deliveries_are_tagged_as_projected_events() {
		var id = Uuid.NewUuid();
		var source = MakeEvent("account-123", 7, "CreditApplied", id, new EsdbPosition(100, 100));
		var link = MakeEvent("$ce-account", 42, "$>", position: new EsdbPosition(200, 200));
		var resolved = new ResolvedEvent(source, link, commitPosition: 200);

		var delivered = GrpcConnectionWrapper.ToRecordedEvent(resolved);

		var projected = Assert.IsType<ProjectedEvent>(delivered);
		Assert.Equal("$ce-account", projected.ProjectedStream);
		Assert.Equal(7, projected.OriginalEventNumber);
		Assert.Equal(42, projected.EventNumber);
		Assert.Equal("account-123", projected.EventStreamId);
		Assert.Equal(id.ToGuid(), projected.EventId);
		Assert.Equal("CreditApplied", projected.EventType);
		Assert.Equal(new byte[] { 1, 2, 3 }, projected.Data);
		Assert.Equal(new byte[] { 4, 5 }, projected.Metadata);
		Assert.True(projected.IsJson);
		Assert.NotNull(projected.Position);
		Assert.Equal(200, projected.Position!.Value.CommitPosition);
		Assert.Equal(200, projected.Position.Value.PreparePosition);
		Assert.Equal(((DateTimeOffset)source.Created).ToUnixTimeMilliseconds(), projected.CreatedEpoch);
	}

	[Fact]
	public void non_link_deliveries_stay_plain_recorded_events_with_position() {
		var id = Uuid.NewUuid();
		var source = MakeEvent("account-123", 7, "CreditApplied", id, new EsdbPosition(55, 55));
		var resolved = new ResolvedEvent(source, link: null, commitPosition: 55);

		var delivered = GrpcConnectionWrapper.ToRecordedEvent(resolved);

		Assert.NotNull(delivered);
		Assert.IsNotType<ProjectedEvent>(delivered);
		Assert.Equal("account-123", delivered!.EventStreamId);
		Assert.Equal(7, delivered.EventNumber);
		Assert.Equal(id.ToGuid(), delivered.EventId);
		Assert.NotNull(delivered.Position);
		Assert.Equal(55, delivered.Position!.Value.CommitPosition);
		Assert.Equal(55, delivered.Position.Value.PreparePosition);
		Assert.Equal(((DateTimeOffset)source.Created).ToUnixTimeMilliseconds(), delivered.CreatedEpoch);
	}

	[Fact]
	public void created_epoch_is_unix_milliseconds_not_ticks() {
		var source = MakeEvent("s", 0, "T");
		var resolved = new ResolvedEvent(source, null, commitPosition: 1);
		var delivered = GrpcConnectionWrapper.ToRecordedEvent(resolved)!;

		Assert.NotEqual(source.Created.Ticks, delivered.CreatedEpoch);
		Assert.Equal(((DateTimeOffset)source.Created).ToUnixTimeMilliseconds(), delivered.CreatedEpoch);
	}
}
