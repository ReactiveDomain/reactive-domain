using ReactiveDomain;
using Xunit;

namespace ReactiveDomain.Testing.Tests.StreamStore;

// Positions are unsigned in the store and carried here in a signed long, so End's -1 is the store's
// largest position wearing a minus sign. Ordering them as signed puts the end of the log first.
// ReSharper disable once InconsistentNaming
public sealed class when_ordering_positions {
	private static readonly Position Early = new(512, 512);
	private static readonly Position Late = new(4096, 4096);

	[Fact]
	public void the_end_of_the_log_follows_the_start_of_it() {
		Assert.True(Position.End > Position.Start);
		Assert.False(Position.End < Position.Start);
		Assert.True(Position.Start < Position.End);
	}

	[Fact]
	public void the_end_of_the_log_follows_every_real_position() {
		Assert.True(Position.End > Early);
		Assert.True(Position.End > Late);
		Assert.True(Late < Position.End);
	}

	[Fact]
	public void the_start_of_the_log_precedes_every_real_position() {
		Assert.True(Position.Start < Early);
		Assert.True(Early > Position.Start);
	}

	[Fact]
	public void real_positions_order_by_commit_then_prepare() {
		Assert.True(Early < Late);
		Assert.True(new Position(512, 8) < new Position(512, 9));
		Assert.True(new Position(512, 9) > new Position(512, 8));
	}

	[Fact]
	public void a_position_is_neither_before_nor_after_its_equal() {
		var same = new Position(Early.CommitPosition, Early.PreparePosition);
		Assert.False(Early < same);
		Assert.False(Early > same);
		Assert.True(Early <= same);
		Assert.True(Early >= same);
	}

	[Fact]
	public void positions_can_be_sorted() {
		// Comparer<Position>.Default has nothing to work with unless Position says how it orders, so
		// this threw rather than sorting wrongly.
		var sorted = new List<Position> { Position.End, Late, Position.Start, Early };
		sorted.Sort();

		Assert.Equal([Position.Start, Early, Late, Position.End], sorted);
	}

	[Fact]
	public void ordering_agrees_with_equality() {
		Assert.Equal(0, Early.CompareTo(Early));
		Assert.True(Early.CompareTo(Late) < 0);
		Assert.True(Late.CompareTo(Early) > 0);
		Assert.Equal(0, Position.End.CompareTo(Position.End));
	}

	[Fact]
	public void the_end_sentinel_is_the_store_encoding_it_round_trips_as() {
		// The reason ordering has to be unsigned: this is ulong.MaxValue in the wrappers' cast, which
		// is the store's own end-of-log, not a position below zero.
		Assert.Equal(-1L, Position.End.CommitPosition);
		Assert.Equal(ulong.MaxValue, (ulong)Position.End.CommitPosition);
	}
}
