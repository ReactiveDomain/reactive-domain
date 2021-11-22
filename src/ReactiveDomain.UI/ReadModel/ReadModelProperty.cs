using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace ReactiveDomain.UI
{
    /// <summary>
    /// A wrapper for an observable provider, intended for use in transient read models.
    /// This provider is a hot observable. Includes simple semantics for appending a new
    /// item to the provider. Late subscribers are automatically provided with the most
    /// recent value upon subscription.
    /// </summary>
    /// <typeparam name="T">The type of data for the provider.</typeparam>
    public class ReadModelProperty<T> : IObservable<T>
    {
        private readonly List<IObserver<T>> _subscribed = new List<IObserver<T>>();
        private readonly IObservable<T> _observable;

        /// <summary>
        /// Creates a new <see cref="ReadModelProperty{T}"/>.
        /// </summary>
        /// <param name="startValue">The provider's initial value.</param>
        /// <param name="publishWrapper">An optional wrapper to use when providing
        /// new values to subscribers.</param>
        public ReadModelProperty(T startValue, Action<Action> publishWrapper = null)
        {
            _lastValue = startValue;
            _publishWrapper = publishWrapper;
            _observable = Observable.Create<T>(o =>
            {
                _subscribed.Add(o);
                return () => _subscribed.Remove(o);
            });
        }

        private T _lastValue;
        private readonly Action<Action> _publishWrapper;

        /// <summary>
        /// Append an item to the provider's stream and notifies all subscribers.
        /// The stream is unchanged and no notifications are made if the provided
        /// value is the same as the previous value.
        /// </summary>
        /// <param name="val">The value to be appended to the stream.</param>
        /// <param name="force">Forces notifications. If true, notifies subscribers of
        /// the new value even if that value is the same as the previous value.</param>
        public void Update(T val, bool force = false)
        {
            var noChange = (val != null && val.Equals(_lastValue)) || (val == null && _lastValue == null);
            if (!force && noChange) return;
            _lastValue = val;
            var subscribed = _subscribed.ToArray();
            // ReSharper disable once ForCanBeConvertedToForeach - If someone throws they will be removed form the collection
            for (var i = 0; i < subscribed.Length; i++)
            {
                var subscriber = subscribed[i];
                if (_publishWrapper != null)
                    _publishWrapper(() => subscriber.OnNext(val));
                else
                    subscriber.OnNext(val);
            }
        }

        /// <summary>
        /// Subscribes a new observer to this observable and asynchronously notifies
        /// that observer of the most recent value.
        /// </summary>
        /// <param name="observer">The observer to subscribe.</param>
        /// <returns>A reference to an interface that allows observers to stop receiving
        /// notifications before the provider has finished sending them.</returns>
        public IDisposable Subscribe(IObserver<T> observer)
        {
            var unsubscribe = _observable.Subscribe(observer);
            Task.Run(() => observer.OnNext(_lastValue));
            return unsubscribe;
        }
    }
}
