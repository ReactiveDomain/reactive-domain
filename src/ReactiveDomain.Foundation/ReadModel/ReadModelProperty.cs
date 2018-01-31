using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace ReactiveDomain.Foundation.ReadModel
{

    public class ReadModelProperty<T> : IObservable<T>
    {
        private readonly List<IObserver<T>> _subscribed = new List<IObserver<T>>();
        private readonly IObservable<T> _observable;
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
        public IDisposable Subscribe(IObserver<T> observer)
        {
            var unsubscribe = _observable.Subscribe(observer);
            Task.Run(() => observer.OnNext(_lastValue));
            return unsubscribe;
        }

        public static implicit operator ReadModelProperty<T>(bool v)
        {
            throw new NotImplementedException();
        }
    }
}
