using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging;
using ReactiveDomain.Testing.EventStore;
using Xunit;

namespace ReactiveDomain.Testing.Tests.StreamStore;

// A position resumes the log the way a checkpoint resumes a stream: it names what has been seen, so
// what comes back is what follows it. Nothing exercised this before — the only caller in the tree
// passes Position.Start, where every arithmetic error here happens to cancel out.
// ReSharper disable once InconsistentNaming
public class when_subscribing_to_all_from_a_position {
	private readonly MockStreamStoreConnection _conn;
	private readonly JsonMessageSerializer _serializer = new();
	private readonly string _stream = $"allPositionTest-{Guid.NewGuid():N}";

	public when_subscribing_to_all_from_a_position() {
		_conn = new MockStreamStoreConnection(nameof(when_subscribing_to_all_from_a_position));
		_conn.Connect();
		for (var i = 0; i < 4; i++) {
			_conn.AppendToStream(_stream, ExpectedVersion.Any, null,
				_serializer.Serialize(new AllPositionTestEvent(i)));
		}
	}

	private List<RecordedEvent> ReadAllFrom(Position from) {
		var seen = new List<RecordedEvent>();
		using (_conn.SubscribeToAllFrom(from, e => seen.Add(e))) { }
		return seen;
	}

	private List<RecordedEvent> Everything() {
		var seen = new List<RecordedEvent>();
		using (_conn.SubscribeToAll(e => seen.Add(e))) { }
		return seen;
	}

	[Fact]
	public void the_start_of_the_log_yields_every_entry() {
		Assert.Equal(Everything().Count, ReadAllFrom(Position.Start).Count);
	}

	[Fact]
	public void a_position_yields_exactly_the_entries_after_it() {
		var all = Everything();

		// Every prefix, so an off-by-one anywhere in the log shows up rather than hiding at an end.
		for (var i = 0; i < all.Count; i++) {
			var resumed = ReadAllFrom(all[i].Position!.Value);
			Assert.Equal(all.Count - (i + 1), resumed.Count);
			if (resumed.Count > 0) {
				Assert.Equal(all[i + 1].Position, resumed[0].Position);
			}
		}
	}

	[Fact]
	public void the_last_position_yields_nothing() {
		var all = Everything();
		Assert.Empty(ReadAllFrom(all[^1].Position!.Value));
	}

	[Fact]
	public void the_end_of_the_log_yields_nothing_already_written() {
		Assert.Empty(ReadAllFrom(Position.End));
	}

	[Fact]
	public void entries_come_back_in_log_order() {
		var resumed = ReadAllFrom(Position.Start);
		for (var i = 1; i < resumed.Count; i++) {
			Assert.True(resumed[i].Position > resumed[i - 1].Position,
				$"entry {i} at {resumed[i].Position} does not follow {resumed[i - 1].Position}");
		}
	}

	[Fact]
	public void resuming_never_skips_and_never_repeats_an_entry() {
		var all = Everything();

		// Walk the log one entry at a time, as a consumer resuming from its own checkpoint would.
		var walked = new List<Position>();
		var cursor = Position.Start;
		while (true) {
			var next = ReadAllFrom(cursor).FirstOrDefault();
			if (next is null) { break; }
			walked.Add(next.Position!.Value);
			cursor = next.Position!.Value;
		}

		Assert.Equal(all.Select(e => e.Position!.Value), walked);
	}

	public record AllPositionTestEvent(int Number) : Event;
}
