using ReactiveDomain.Foundation;
using Xunit;

namespace ReactiveDomain.Testing;

/// <summary>Route-table assertions for aggregates under test.</summary>
public static class AggregateRoutes {
	/// <summary>
	/// Fails when a type in <paramref name="raised"/> has neither a <c>Register</c> nor a
	/// <c>RegisterPassThrough</c> on <paramref name="aggregate"/>.
	/// </summary>
	/// <param name="aggregate">The instance whose route table is inspected.</param>
	/// <param name="raised">Every event type the aggregate's command methods raise.</param>
	/// <param name="passThrough">Types raised for downstream readers and never folded — must also be
	/// registered via <c>RegisterPassThrough</c>, or listed here if the instance cannot register them.</param>
	public static void AssertCovered(
		EventDrivenStateMachine aggregate,
		IEnumerable<Type> raised,
		params Type[] passThrough) {
		ArgumentNullException.ThrowIfNull(aggregate);
		ArgumentNullException.ThrowIfNull(raised);
		var covered = new HashSet<Type>(aggregate.RegisteredEventTypes);
		foreach (var type in passThrough) {
			covered.Add(type);
		}
		var missing = raised.Where(t => !covered.Contains(t)).Select(t => t.FullName).ToList();
		Assert.True(
			missing.Count == 0,
			$"{aggregate.GetType().Name} raises [{string.Join(", ", missing)}] with no route. " +
			"Register a handler, or RegisterPassThrough if the event is persisted for downstream " +
			"readers and never folded.");
	}
}
