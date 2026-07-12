using System.Reactive.Linq;

// ReSharper disable once CheckNamespace
namespace ReactiveDomain.Foundation;

/// <summary>
/// A wrapper for an observable provider, intended for use in transient read models.
/// This provider is a hot observable. Includes simple semantics for appending a new
/// item to the provider. Late subscribers are synchronously provided with the most
/// recent value upon subscription.
/// <br/>
/// All deliveries (initial and update) are serialized through a single lock, so a
/// subscriber can never observe an older value after a newer one. Because the current
/// value is delivered synchronously inside <see cref="Subscribe"/>, observer callbacks
/// may re-enter this property (e.g. call <see cref="Update"/>) on the same thread, but
/// must not block waiting on another thread that also uses this property. If a
/// publish wrapper is provided it should enqueue rather than block for the same reason.
/// </summary>
/// <typeparam name="T">The type of data for the provider.</typeparam>
public class ReadModelProperty<T> : IObservable<T> {
	private readonly object _gate = new();
	private readonly List<IObserver<T>> _subscribed = [];
	private readonly IObservable<T> _observable;

	/// <summary>
	/// Creates a new <see cref="ReadModelProperty{T}"/>.
	/// </summary>
	/// <param name="startValue">The provider's initial value.</param>
	/// <param name="publishWrapper">An optional wrapper to use when providing
	/// new values to subscribers.</param>
	public ReadModelProperty(T startValue, Action<Action>? publishWrapper = null) {
		_lastValue = startValue;
		_publishWrapper = publishWrapper;
		_observable = Observable.Create<T>(o => {
			lock (_gate) {
				_subscribed.Add(o);
			}
			return () => {
				lock (_gate) {
					_subscribed.Remove(o);
				}
			};
		});
	}

	private T _lastValue;
	private readonly Action<Action>? _publishWrapper;

	/// <summary>
	/// The most recent value, read under the same lock that orders deliveries.
	/// Exposed for test visibility via ReactiveDomain.Testing; production code
	/// should subscribe rather than poll.
	/// </summary>
	internal T CurrentValue {
		get {
			lock (_gate) {
				return _lastValue;
			}
		}
	}

	/// <summary>
	/// Append an item to the provider's stream and notifies all subscribers.
	/// The stream is unchanged and no notifications are made if the provided
	/// value is the same as the previous value.
	/// </summary>
	/// <param name="val">The value to be appended to the stream.</param>
	/// <param name="force">Forces notifications. If true, notifies subscribers of
	/// the new value even if that value is the same as the previous value.</param>
	public void Update(T val, bool force = false) {
		lock (_gate) {
			var noChange = val != null && val.Equals(_lastValue) || val == null && _lastValue == null;
			if (!force && noChange)
				return;
			_lastValue = val;
			var subscribed = _subscribed.ToArray();
			// ReSharper disable once ForCanBeConvertedToForeach - If someone throws they will be removed form the collection
			for (var i = 0; i < subscribed.Length; i++) {
				Notify(subscribed[i], val);
			}
		}
	}

	/// <summary>
	/// Subscribes a new observer to this observable and synchronously notifies
	/// that observer of the most recent value. The initial delivery is ordered
	/// with respect to <see cref="Update"/> notifications, so the observer can
	/// never see an older value after a newer one.
	/// </summary>
	/// <param name="observer">The observer to subscribe.</param>
	/// <returns>A reference to an interface that allows observers to stop receiving
	/// notifications before the provider has finished sending them.</returns>
	public IDisposable Subscribe(IObserver<T> observer) {
		lock (_gate) {
			var unsubscribe = _observable.Subscribe(observer);
			Notify(observer, _lastValue);
			return unsubscribe;
		}
	}

	private void Notify(IObserver<T> observer, T val) {
		if (_publishWrapper != null)
			_publishWrapper(() => observer.OnNext(val));
		else
			observer.OnNext(val);
	}
}
