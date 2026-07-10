// ReSharper disable once CheckNamespace
namespace ReactiveDomain;

/// <summary>
/// Routes events to logic in an event source.
/// </summary>
public class EventRouter {
	private readonly Dictionary<Type, Action<object>> _routes;

	/// <summary>
	/// Initializes a new event router.
	/// </summary>
	public EventRouter() {
		_routes = new Dictionary<Type, Action<object>>();
	}

	/// <summary>
	/// Registers where to route the specified <typeparamref name="TEvent">type of event</typeparamref> to.
	/// </summary>
	/// <typeparam name="TEvent">The type of event the route is being registered for.</typeparam>
	/// <param name="route">The logic that knows what to do when an event of this type is routed.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="route"/> is <c>null</c>.</exception>
	/// <exception cref="InvalidOperationException">Thrown when you try to register more than one route for the specified <typeparamref name="TEvent">type of event.</typeparamref></exception>
	public void RegisterRoute<TEvent>(Action<TEvent> route) {
		ArgumentNullException.ThrowIfNull(route);

		var typeOfEvent = typeof(TEvent);
		if (_routes.ContainsKey(typeOfEvent))
			throw new InvalidOperationException(
				$"There's already a route registered for the event of type '{typeOfEvent.Name}'");

		_routes.Add(typeOfEvent, @event => route((TEvent)@event));
	}

	/// <summary>
	/// Registers where to route the specified <paramref name="typeOfEvent">type of event</paramref> to.
	/// </summary>
	/// <param name="typeOfEvent">The type of event the route is being registered for.</param>
	/// <param name="route">The logic that knows what to do when an event of this type is routed.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="typeOfEvent"/> or <paramref name="route"/> is <c>null</c>.</exception>
	/// <exception cref="InvalidOperationException">Thrown when you try to register more than one route for the specified <paramref name="typeOfEvent">type of event.</paramref></exception>
	public void RegisterRoute(Type typeOfEvent, Action<object> route) {
		ArgumentNullException.ThrowIfNull(typeOfEvent);
		ArgumentNullException.ThrowIfNull(route);

		if (!_routes.TryAdd(typeOfEvent, route))
			throw new InvalidOperationException(
				$"There's already a route registered for the event of type '{typeOfEvent.Name}'");
	}

	/// <summary>
	/// Routes the specified event (if a route was registered).
	/// </summary>
	/// <param name="event">The event to route.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="event"/> is <c>null</c>.</exception>
	public void Route(object @event) {
		ArgumentNullException.ThrowIfNull(@event);

		if (_routes.TryGetValue(@event.GetType(), out var route)) {
			route(@event);
		}
	}
}
