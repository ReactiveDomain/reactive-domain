using KurrentDB.Client;
using ReactiveDomain.EventStore;
using Xunit;

namespace ReactiveDomain.Testing.Tests.StreamStore;

/// <summary>
/// Pins filter → KurrentDB <see cref="IEventFilter"/> mapping used by
/// <see cref="GrpcConnectionWrapper"/>.
/// </summary>
public sealed class StreamStoreEventFilterMappingTests {
	[Fact]
	public void exclude_system_maps_to_event_type_exclude_system() {
		var filter = GrpcConnectionWrapper.ToClientFilter(StreamStoreEventFilter.ExcludeSystemEvents());
		Assert.IsAssignableFrom<IEventFilter>(filter);
		Assert.Equal(EventTypeFilter.ExcludeSystemEvents().GetType(), filter.GetType());
	}

	[Fact]
	public void event_type_prefix_maps() {
		var filter = GrpcConnectionWrapper.ToClientFilter(
			StreamStoreEventFilter.EventTypePrefix("Credit", "Debit"));
		Assert.Equal(EventTypeFilter.Prefix("Credit", "Debit").GetType(), filter.GetType());
	}

	[Fact]
	public void stream_prefix_maps() {
		var filter = GrpcConnectionWrapper.ToClientFilter(
			StreamStoreEventFilter.StreamPrefix("account-"));
		Assert.Equal(StreamFilter.Prefix("account-").GetType(), filter.GetType());
	}

	[Fact]
	public void event_type_regex_and_stream_regex_map() {
		Assert.Equal(
			EventTypeFilter.RegularExpression("^Foo").GetType(),
			GrpcConnectionWrapper.ToClientFilter(StreamStoreEventFilter.EventTypeRegex("^Foo")).GetType());
		Assert.Equal(
			StreamFilter.RegularExpression("^account-").GetType(),
			GrpcConnectionWrapper.ToClientFilter(StreamStoreEventFilter.StreamRegex("^account-")).GetType());
	}

	[Fact]
	public void filter_factories_set_kind_and_payload() {
		Assert.Equal(StreamStoreEventFilter.FilterKind.ExcludeSystemEvents,
			StreamStoreEventFilter.ExcludeSystemEvents().Kind);
		Assert.Equal(StreamStoreEventFilter.FilterKind.EventTypeRegex,
			StreamStoreEventFilter.EventTypeRegex("^Foo").Kind);
		Assert.Equal("^Foo", StreamStoreEventFilter.EventTypeRegex("^Foo").Regex);
		Assert.Equal(["a", "b"], StreamStoreEventFilter.StreamPrefix("a", "b").Prefixes);
	}
}
