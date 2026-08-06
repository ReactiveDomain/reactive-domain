using Xunit;

namespace ReactiveDomain.Testing.Tests;

public sealed class TestCapacityDetectorTests {
	// A lookup over an in-memory map, so every branch is exercised without mutating real process
	// environment variables — which are shared, order-dependent state across the whole test run.
	private static Func<string, string?> Env(params (string Name, string Value)[] vars) {
		var map = vars.ToDictionary(v => v.Name, v => v.Value, StringComparer.OrdinalIgnoreCase);
		return name => map.TryGetValue(name, out var value) ? value : null;
	}

	private static Func<string, string?> Override(string value) =>
		Env((TestCapacityDetector.OverrideEnvironmentVariable, value));

	[Theory]
	[InlineData(1, TestCapacity.Small)]
	[InlineData(2, TestCapacity.Small)]
	[InlineData(3, TestCapacity.Small)]
	[InlineData(4, TestCapacity.Medium)]
	[InlineData(7, TestCapacity.Medium)]
	[InlineData(8, TestCapacity.Large)]
	[InlineData(128, TestCapacity.Large)]
	public void the_cores_choose_the_bucket(int cores, TestCapacity expected) {
		var (capacity, reported, reason) = TestCapacityDetector.Detect(cores, Env());

		Assert.Equal(expected, capacity);
		Assert.Equal(cores, reported);
		Assert.Contains(cores.ToString(), reason);
	}

	/// <summary>Consumers force against the boundaries, so they are pinned at the edge.</summary>
	[Fact]
	public void each_boundary_is_inclusive_at_its_own_bucket() {
		Assert.Equal(TestCapacity.Large,
			TestCapacityDetector.Detect(TestCapacityDetector.LargeCores, Env()).Capacity);
		Assert.Equal(TestCapacity.Medium,
			TestCapacityDetector.Detect(TestCapacityDetector.LargeCores - 1, Env()).Capacity);
		Assert.Equal(TestCapacity.Medium,
			TestCapacityDetector.Detect(TestCapacityDetector.MediumCores, Env()).Capacity);
		Assert.Equal(TestCapacity.Small,
			TestCapacityDetector.Detect(TestCapacityDetector.MediumCores - 1, Env()).Capacity);
	}

	[Theory]
	[InlineData("Small", TestCapacity.Small)]
	[InlineData("medium", TestCapacity.Medium)]
	[InlineData("LARGE", TestCapacity.Large)]
	public void an_override_wins_over_the_cores(string value, TestCapacity expected) {
		// Cores that would choose a different bucket on their own, so only the override produces this.
		var cores = expected == TestCapacity.Large ? 1 : 64;

		var (capacity, _, reason) = TestCapacityDetector.Detect(cores, Override(value));

		Assert.Equal(expected, capacity);
		Assert.Contains("override", reason);
	}

	/// <summary>The cores are still a real answer; "the override worked" is the costlier thing to believe.</summary>
	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData("true")]
	[InlineData("Ci")]
	public void an_unrecognized_override_falls_through_to_the_cores(string value) {
		Assert.Equal(TestCapacity.Large, TestCapacityDetector.Detect(64, Override(value)).Capacity);
		Assert.Equal(TestCapacity.Small, TestCapacityDetector.Detect(2, Override(value)).Capacity);
	}

	/// <summary>Naming a CI system is what this replaced: a large runner is not small for being CI.</summary>
	[Fact]
	public void a_ci_variable_alone_does_not_shrink_the_bucket() {
		var (capacity, _, _) = TestCapacityDetector.Detect(64, Env(("GITHUB_ACTIONS", "true"), ("CI", "true")));

		Assert.Equal(TestCapacity.Large, capacity);
	}

	[Fact]
	public void the_detected_values_describe_this_process() {
		Assert.Equal(Environment.ProcessorCount, TestCapacityDetector.Cores);
		Assert.False(string.IsNullOrWhiteSpace(TestCapacityDetector.Reason));
		Assert.Equal(TestCapacityDetector.Detected, TestTimeouts.Capacity);
	}
}
