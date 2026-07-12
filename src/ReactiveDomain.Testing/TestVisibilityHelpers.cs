using System.Reflection;
using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging;

namespace ReactiveDomain.Testing;

public static class TestVisibilityExtensions {
	private static T GetInstanceField<T>(object instance, string fieldName) {
		const BindingFlags bindFlags =
			BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
		var field = instance.GetType().GetField(fieldName, bindFlags);
		return (T?)field?.GetValue(instance) ??
			   throw new InvalidOperationException($"{fieldName} not found on {typeof(T).Name}.");
	}

	private static T GetInstanceField<T, V>(V instance, string fieldName) {
		const BindingFlags bindFlags =
			BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
		var field = typeof(V).GetField(fieldName, bindFlags);
		return (T?)field?.GetValue(instance) ??
			   throw new InvalidOperationException($"{fieldName} not found on {typeof(T).Name}.");
	}

	private static void SetInstanceField<T>(object instance, string fieldName, T newValue) {
		const BindingFlags bindFlags =
			BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
		var field = instance.GetType().GetField(fieldName, bindFlags);
		field?.SetValue(instance, newValue);
	}

	/// <param name="model">Read Model Under Test</param>
	extension(ReadModelBase model) {
		/// <summary>
		/// UpdateViaListener should only be used when the read model 
		/// has specialized Listener behavior and UpdateFromAggregate
		/// cannot be used.
		/// </summary>
		/// <param name="name">Listener stream name</param>
		/// <param name="aggregate">Source Aggregate</param>
		/// <exception cref="StreamNotFoundException"></exception>
		public void UpdateViaListener(string name,
			EventDrivenStateMachine? aggregate = null) {
			var listeners = GetInstanceField<List<IListener>, ReadModelBase>(model, "_listeners");
			var listener = listeners.FirstOrDefault(l => l.StreamName == name);
			if (listener == null) { throw new StreamNotFoundException($"Listener {name}"); }
			if (aggregate == null) { return; }
			foreach (IMessage evt in aggregate.TakeEvents()) {
				model.DirectApply(evt);
				SetInstanceField(listener, "_position", listener.Position + 1);
			}
		}

		/// <summary>
		/// UpdateFromAggregate should be used to test read models 
		/// in place of MockRepositories. 
		/// This will update bypassing the queue and Listeners
		/// The ReaderLock will be enforced.
		/// Unsubscribed messages will be silently dropped by the read model.
		/// </summary>
		/// <param name="aggregate">Source Aggregate</param>
		public void UpdateFromAggregate(EventDrivenStateMachine aggregate) {
			foreach (IMessage evt in aggregate.TakeEvents()) {
				model.DirectApply(evt);
			}
		}
	}

	/// <summary>
	/// Allows test to access the current value of a ReadModelProperty.
	/// This leverages the fact that the current value is stored to be published to new subscribers.
	/// The value is read under the same lock that orders deliveries, so it cannot be torn.
	/// </summary>
	/// <typeparam name="T">the ReadModelProperty type</typeparam>
	/// <param name="readModelProperty">the ReadModelProperty</param>
	/// <returns></returns>
	public static T CurrentValue<T>(this IObservable<T> readModelProperty) {
		return readModelProperty is ReadModelProperty<T> property
			? property.CurrentValue
			: throw new InvalidOperationException("Observable is not a ReadModelProperty.");
	}
}
