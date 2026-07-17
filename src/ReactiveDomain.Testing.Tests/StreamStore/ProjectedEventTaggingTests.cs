using System.Runtime.CompilerServices;
using ReactiveDomain.EventStore;
using Xunit;
using ES = EventStore.ClientAPI;

namespace ReactiveDomain.Testing.Tests.StreamStore;

/// <summary>
/// Pins the $all delivery mapping of EventStoreConnectionWrapper: link-resolved deliveries are
/// tagged ProjectedEvent with the same field semantics as MockStreamStoreConnection's emulated
/// projection copies. ClientAPI's RecordedEvent/ResolvedEvent have internal constructors, so the
/// inputs are built via reflection.
/// </summary>
public sealed class ProjectedEventTaggingTests {
	private static ES.RecordedEvent MakeEsEvent(string stream, long number, string type, Guid id) {
		var evt = (ES.RecordedEvent)RuntimeHelpers.GetUninitializedObject(typeof(ES.RecordedEvent));
		Set(evt, nameof(ES.RecordedEvent.EventStreamId), stream);
		Set(evt, nameof(ES.RecordedEvent.EventId), id);
		Set(evt, nameof(ES.RecordedEvent.EventNumber), number);
		Set(evt, nameof(ES.RecordedEvent.EventType), type);
		Set(evt, nameof(ES.RecordedEvent.Data), new byte[] { 1, 2, 3 });
		Set(evt, nameof(ES.RecordedEvent.Metadata), new byte[] { 4, 5 });
		Set(evt, nameof(ES.RecordedEvent.IsJson), true);
		Set(evt, nameof(ES.RecordedEvent.Created), new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc));
		Set(evt, nameof(ES.RecordedEvent.CreatedEpoch), 1767323045000L);
		return evt;

		static void Set(object target, string field, object value) =>
			typeof(ES.RecordedEvent).GetField(field)!.SetValue(target, value);
	}

	private static ES.ResolvedEvent MakeResolved(ES.RecordedEvent? evt, ES.RecordedEvent? link) {
		object boxed = RuntimeHelpers.GetUninitializedObject(typeof(ES.ResolvedEvent));
		typeof(ES.ResolvedEvent).GetField(nameof(ES.ResolvedEvent.Event))!.SetValue(boxed, evt);
		typeof(ES.ResolvedEvent).GetField(nameof(ES.ResolvedEvent.Link))!.SetValue(boxed, link);
		return (ES.ResolvedEvent)boxed;
	}

	[Fact]
	public void link_resolved_deliveries_are_tagged_as_projected_events() {
		var id = Guid.NewGuid();
		var source = MakeEsEvent("account-123", 7, "CreditApplied", id);
		var link = MakeEsEvent("$ce-account", 42, "$>", Guid.NewGuid());

		var delivered = MakeResolved(source, link).ToDeliveredEvent();

		var projected = Assert.IsType<ProjectedEvent>(delivered);
		Assert.Equal("$ce-account", projected.ProjectedStream);   // the stream this copy lives in
		Assert.Equal(7, projected.OriginalEventNumber);           // position in the source stream
		Assert.Equal(42, projected.EventNumber);                  // position in the projected stream
		Assert.Equal("account-123", projected.EventStreamId);     // source stream, not the link's
		Assert.Equal(id, projected.EventId);
		Assert.Equal("CreditApplied", projected.EventType);
		Assert.Equal(new byte[] { 1, 2, 3 }, projected.Data);
		Assert.Equal(new byte[] { 4, 5 }, projected.Metadata);
		Assert.True(projected.IsJson);
	}

	[Fact]
	public void non_link_deliveries_stay_plain_recorded_events() {
		var id = Guid.NewGuid();
		var source = MakeEsEvent("account-123", 7, "CreditApplied", id);

		var delivered = MakeResolved(source, link: null).ToDeliveredEvent();

		Assert.NotNull(delivered);
		Assert.IsNotType<ProjectedEvent>(delivered);
		Assert.Equal("account-123", delivered!.EventStreamId);
		Assert.Equal(7, delivered.EventNumber);
		Assert.Equal(id, delivered.EventId);
	}

	[Fact]
	public void null_link_targets_are_skipped() {
		// a link whose target was deleted or scavenged resolves with Event == null
		var link = MakeEsEvent("$et-CreditApplied", 3, "$>", Guid.NewGuid());
		Assert.Null(MakeResolved(evt: null, link).ToDeliveredEvent());
	}
}
