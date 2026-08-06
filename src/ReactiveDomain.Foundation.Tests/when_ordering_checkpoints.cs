using Xunit;

namespace ReactiveDomain.Foundation.Tests;

// Checkpoints over several streams form a partial order, not a sequence. These are the laws that
// makes it one, and the case that stops it being one if the comparison goes through a single number.
// ReSharper disable once InconsistentNaming
public sealed class when_ordering_checkpoints {
	private static List<StreamCheckpoint> At(params (string Stream, long? Version)[] streams) =>
		streams.Select(s => new StreamCheckpoint(s.Stream, s.Version)).ToList();

	private static CheckpointOrder Compare(
		IEnumerable<StreamCheckpoint>? first, IEnumerable<StreamCheckpoint>? second) =>
		StreamCheckpoint.Compare(first, second);

	[Fact]
	public void a_set_is_equal_to_itself() {
		var checkpoints = At(("a", 3), ("b", 7));
		Assert.Equal(CheckpointOrder.Equal, Compare(checkpoints, checkpoints));
	}

	[Fact]
	public void covering_nothing_is_equal_however_it_is_expressed() {
		Assert.Equal(CheckpointOrder.Equal, Compare(null, []));
		Assert.Equal(CheckpointOrder.Equal, Compare([], null));
		// A stream that has delivered nothing covers what a stream nobody mentioned covers.
		Assert.Equal(CheckpointOrder.Equal, Compare(At(("a", null)), []));
	}

	[Fact]
	public void further_along_a_stream_is_later() {
		Assert.Equal(CheckpointOrder.Before, Compare(At(("a", 3)), At(("a", 4))));
		Assert.Equal(CheckpointOrder.After, Compare(At(("a", 4)), At(("a", 3))));
	}

	[Fact]
	public void nothing_applied_precedes_the_first_event() {
		// Null is not zero: zero covers the first event, null covers none of them.
		Assert.Equal(CheckpointOrder.Before, Compare(At(("a", null)), At(("a", 0))));
	}

	[Fact]
	public void a_stream_the_other_does_not_mention_is_a_stream_it_covers_nothing_of() {
		// So starting another stream moves a model strictly forward rather than out of the order.
		Assert.Equal(CheckpointOrder.Before, Compare(At(("a", 3)), At(("a", 3), ("b", 0))));
		Assert.Equal(CheckpointOrder.After, Compare(At(("a", 3), ("b", 0)), At(("a", 3))));
	}

	[Fact]
	public void ahead_on_one_stream_and_behind_on_another_is_neither() {
		// The case a single projected position cannot express, and would report as an order.
		Assert.Equal(CheckpointOrder.Concurrent, Compare(At(("a", 5), ("b", 1)), At(("a", 1), ("b", 5))));
	}

	[Fact]
	public void the_order_is_antisymmetric() {
		(List<StreamCheckpoint> First, List<StreamCheckpoint> Second)[] pairs = [
			(At(("a", 1)), At(("a", 2))),
			(At(("a", 1), ("b", 1)), At(("a", 2), ("b", 2))),
			(At(("a", 5), ("b", 1)), At(("a", 1), ("b", 5))),
			(At(("a", 1)), At(("a", 1))),
			(At(("a", null)), At(("a", 0)))
		];
		foreach (var (first, second) in pairs) {
			var forward = Compare(first, second);
			var backward = Compare(second, first);
			var expected = forward switch {
				CheckpointOrder.Before => CheckpointOrder.After,
				CheckpointOrder.After => CheckpointOrder.Before,
				_ => forward // Equal and Concurrent each read the same from either side
			};
			Assert.Equal(expected, backward);
		}
	}

	[Fact]
	public void the_order_is_transitive() {
		var first = At(("a", 1), ("b", 1));
		var second = At(("a", 2), ("b", 1));
		var third = At(("a", 2), ("b", 9));

		Assert.Equal(CheckpointOrder.Before, Compare(first, second));
		Assert.Equal(CheckpointOrder.Before, Compare(second, third));
		Assert.Equal(CheckpointOrder.Before, Compare(first, third));
	}

	[Fact]
	public void a_position_is_not_needed_to_order_a_stream() {
		// Versions are dense and always present; positions are neither, and within a stream they agree.
		var withPosition = new List<StreamCheckpoint> { new("a", 3, new Position(512, 512)) };
		var without = At(("a", 4));
		Assert.Equal(CheckpointOrder.Before, Compare(withPosition, without));
	}

	[Fact]
	public void a_snapshot_orders_by_every_stream_that_reached_it() {
		var earlier = new ReadModelState("m", At(("own", 3)), new object(), At(("relayed", 1)));
		var later = new ReadModelState("m", At(("own", 3)), new object(), At(("relayed", 2)));

		// The relay having fed more of its stream is later, however that stream reached the model.
		Assert.Equal(CheckpointOrder.Before, earlier.Compare(later));
		Assert.Equal(CheckpointOrder.After, later.Compare(earlier));
	}
}
