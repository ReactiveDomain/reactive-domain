using ReactiveDomain.Testing;
using Xunit;

namespace ReactiveDomain.Foundation.Tests;

// ReSharper disable once InconsistentNaming
public sealed class when_using_read_model_property {

	private sealed class RecordingObserver<T> : IObserver<T> {
		public readonly List<T> Received = [];
		public void OnNext(T value) {
			lock (Received) {
				Received.Add(value);
			}
		}
		public void OnError(Exception error) { }
		public void OnCompleted() { }
	}

	[Fact]
	public void subscribers_receive_the_current_value_synchronously_on_subscribe() {
		var property = new ReadModelProperty<int>(5);
		var observer = new RecordingObserver<int>();

		using var _ = property.Subscribe(observer);

		// No waiting: the initial value must be delivered before Subscribe returns.
		Assert.Equal(new[] { 5 }, observer.Received);
	}

	[Fact]
	public void late_subscribers_receive_the_most_recent_value() {
		var property = new ReadModelProperty<int>(0);
		property.Update(42);
		var observer = new RecordingObserver<int>();

		using var _ = property.Subscribe(observer);

		Assert.Equal(new[] { 42 }, observer.Received);
	}

	[Fact]
	public void initial_delivery_uses_the_publish_wrapper() {
		var wrapped = 0;
		var property = new ReadModelProperty<int>(7, publish => {
			wrapped++;
			publish();
		});
		var observer = new RecordingObserver<int>();

		using var _ = property.Subscribe(observer);

		Assert.Equal(1, wrapped);
		Assert.Equal(new[] { 7 }, observer.Received);

		property.Update(8);

		Assert.Equal(2, wrapped);
		Assert.Equal(new[] { 7, 8 }, observer.Received);
	}

	[Fact]
	public void updates_notify_all_subscribers() {
		var property = new ReadModelProperty<int>(0);
		var observer1 = new RecordingObserver<int>();
		var observer2 = new RecordingObserver<int>();
		using var _1 = property.Subscribe(observer1);
		using var _2 = property.Subscribe(observer2);

		property.Update(1);
		property.Update(2);

		Assert.Equal(new[] { 0, 1, 2 }, observer1.Received);
		Assert.Equal(new[] { 0, 1, 2 }, observer2.Received);
	}

	[Fact]
	public void unchanged_values_are_not_delivered_unless_forced() {
		var property = new ReadModelProperty<int>(3);
		var observer = new RecordingObserver<int>();
		using var _ = property.Subscribe(observer);

		property.Update(3);
		Assert.Equal(new[] { 3 }, observer.Received);

		property.Update(3, force: true);
		Assert.Equal(new[] { 3, 3 }, observer.Received);
	}

	[Fact]
	public void disposed_subscribers_are_not_notified() {
		var property = new ReadModelProperty<int>(0);
		var observer = new RecordingObserver<int>();
		var subscription = property.Subscribe(observer);

		property.Update(1);
		subscription.Dispose();
		property.Update(2);

		Assert.Equal(new[] { 0, 1 }, observer.Received);
	}

	[Fact]
	public void current_value_reflects_the_latest_update() {
		var property = new ReadModelProperty<Guid>(Guid.Empty);
		Assert.Equal(Guid.Empty, property.CurrentValue());

		var id = Guid.NewGuid();
		property.Update(id);
		Assert.Equal(id, property.CurrentValue());
	}

	[Fact]
	public async Task subscribers_never_observe_an_older_value_after_a_newer_one() {
		// Regression test for GH-212: the initial delivery raced Update and could
		// stomp a subscriber with a stale value after a newer one was delivered.
		var property = new ReadModelProperty<long>(0);
		const int updates = 10_000;
		const int subscribers = 200;
		var failures = 0;

		var writer = Task.Run(() => {
			for (long i = 1; i <= updates; i++) {
				property.Update(i);
			}
		});

		var readers = Task.Run(() => {
			for (var i = 0; i < subscribers; i++) {
				var last = long.MinValue;
				var subscription = property.Subscribe(new AdHocObserver<long>(v => {
					if (v < Interlocked.Read(ref last))
						Interlocked.Increment(ref failures);
					Interlocked.Exchange(ref last, v);
				}));
				subscription.Dispose();
			}
		});

		await Task.WhenAll(writer, readers);
		Assert.Equal(0, failures);
	}

	private sealed class AdHocObserver<T>(Action<T> onNext) : IObserver<T> {
		public void OnNext(T value) => onNext(value);
		public void OnError(Exception error) { }
		public void OnCompleted() { }
	}
}
