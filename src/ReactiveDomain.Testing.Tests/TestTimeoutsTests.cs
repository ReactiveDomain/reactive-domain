using Xunit;

namespace ReactiveDomain.Testing.Tests;

/// <summary>
/// Every test that assigns <see cref="TestTimeouts.Budget"/> lives in this one class and restores it,
/// because it is process-wide: xunit serializes tests within a class but runs classes in parallel, so
/// a reader in another class could otherwise see a budget this one was midway through swapping.
/// </summary>
public sealed class TestTimeoutsTests {
	private static void WithBudget(TestTimeoutBudget budget, Action assert) {
		var original = TestTimeouts.Budget;
		try {
			TestTimeouts.Budget = budget;
			assert();
		} finally {
			TestTimeouts.Budget = original;
		}
	}

	[Fact]
	public void the_budget_defaults_to_the_detected_bucket() {
		Assert.Equal(TestTimeoutBudget.For(TestTimeouts.Capacity), TestTimeouts.Budget);
	}

	/// <summary>The three waits read the budget rather than caching it, or tuning would not take.</summary>
	[Fact]
	public void assigning_a_budget_retunes_the_waits() {
		var tuned = new TestTimeoutBudget(
			TimeSpan.FromSeconds(11), TimeSpan.FromSeconds(22), TimeSpan.FromSeconds(33));

		WithBudget(tuned, () => {
			Assert.Equal(tuned.WaitFor, TestTimeouts.WaitFor);
			Assert.Equal(tuned.CommandTimeout, TestTimeouts.CommandTimeout);
			Assert.Equal(tuned.ThrottleWaitFor, TestTimeouts.ThrottleWaitFor);
		});

		Assert.Equal(TestTimeoutBudget.For(TestTimeouts.Capacity), TestTimeouts.Budget);
	}

	[Theory]
	[InlineData(TestCapacity.Large, 500, 500, 2000)]
	[InlineData(TestCapacity.Medium, 2000, 5000, 5000)]
	[InlineData(TestCapacity.Small, 5000, 10_000, 10_000)]
	public void the_shipped_defaults_are_what_they_say(
		TestCapacity capacity, int waitForMs, int commandMs, int throttleMs) {
		var budget = TestTimeoutBudget.For(capacity);

		Assert.Equal(TimeSpan.FromMilliseconds(waitForMs), budget.WaitFor);
		Assert.Equal(TimeSpan.FromMilliseconds(commandMs), budget.CommandTimeout);
		Assert.Equal(TimeSpan.FromMilliseconds(throttleMs), budget.ThrottleWaitFor);
	}

	/// <summary>A smaller bucket never waits less, whatever the numbers are tuned to.</summary>
	[Fact]
	public void the_defaults_do_not_shrink_as_the_bucket_does() {
		var large = TestTimeoutBudget.For(TestCapacity.Large);
		var medium = TestTimeoutBudget.For(TestCapacity.Medium);
		var small = TestTimeoutBudget.For(TestCapacity.Small);

		Assert.True(medium.WaitFor >= large.WaitFor && small.WaitFor >= medium.WaitFor);
		Assert.True(medium.CommandTimeout >= large.CommandTimeout && small.CommandTimeout >= medium.CommandTimeout);
		Assert.True(medium.ThrottleWaitFor >= large.ThrottleWaitFor && small.ThrottleWaitFor >= medium.ThrottleWaitFor);
	}

	[Fact]
	public void the_legacy_flag_tracks_the_smallest_bucket() {
		Assert.Equal(TestTimeouts.Capacity == TestCapacity.Small, TestTimeouts.IsCi);
	}

	[Fact]
	public void the_description_names_the_bucket_and_the_budget_in_force() {
		var tuned = new TestTimeoutBudget(
			TimeSpan.FromSeconds(11), TimeSpan.FromSeconds(22), TimeSpan.FromSeconds(33));

		WithBudget(tuned, () => {
			var description = TestTimeouts.CapacityDescription;

			Assert.Contains(TestTimeouts.Capacity.ToString(), description);
			Assert.Contains(TestTimeouts.CapacityReason, description);
			Assert.Contains(tuned.WaitFor.ToString(), description);
		});
	}

	[Fact]
	public void the_description_can_be_written_somewhere_other_than_the_console() {
		var writer = new StringWriter();

		TestTimeouts.WriteCapacity(writer);

		Assert.Contains(TestTimeouts.CapacityDescription, writer.ToString());
	}
}
