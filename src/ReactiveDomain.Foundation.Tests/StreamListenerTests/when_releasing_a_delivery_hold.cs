using ReactiveDomain.Testing;
using ReactiveDomain.Testing.EventStore;
using Xunit;

namespace ReactiveDomain.Foundation.Tests.StreamListenerTests;

/// <summary>
/// A hold stops the listener's delivery thread, so a release that does not happen is a listener that
/// never delivers again. These pin that a release either happens or says it did not.
/// </summary>
/// <remarks>
/// Every off-thread step runs on a dedicated <see cref="Thread"/>. A task blocked on with
/// <c>Wait</c> can be inlined onto the waiting thread, which for a thread-affine lock is the one
/// thing these tests must not let happen.
/// </remarks>
// ReSharper disable once InconsistentNaming
public sealed class when_releasing_a_delivery_hold : IDisposable {
	private readonly MockStreamStoreConnection _conn;
	private readonly StreamListener _listener;

	public when_releasing_a_delivery_hold() {
		_conn = new MockStreamStoreConnection(nameof(when_releasing_a_delivery_hold));
		_conn.Connect();
		_listener = new StreamListener(
			nameof(when_releasing_a_delivery_hold),
			_conn,
			new PrefixedCamelCaseStreamNameBuilder(nameof(when_releasing_a_delivery_hold)),
			new JsonMessageSerializer());
	}

	public void Dispose() {
		_listener.Dispose();
		_conn.Dispose();
	}

	/// <summary>Whatever <paramref name="work"/> threw, run on a thread of its own.</summary>
	private static Exception? ThrownOffThread(Action work) {
		Exception? thrown = null;
		var thread = new Thread(() => {
			try { work(); } catch (Exception ex) { thrown = ex; }
		}) { IsBackground = true };
		thread.Start();
		Assert.True(thread.Join(TestTimeouts.ThrottleWaitFor), "The off-thread step did not finish.");
		return thrown;
	}

	/// <summary>
	/// Whether a fresh hold can be taken, which is true only if the last one was released. Probed from
	/// another thread, since the lock is re-entrant and its holder would always succeed.
	/// </summary>
	private bool DeliveryIsFree() {
		using var acquired = new ManualResetEventSlim(false);
		// Released as soon as it is taken, so a probe that is still blocked when this returns false
		// cannot hold anything up once the real holder lets go.
		new Thread(() => {
			using (_listener.HoldDelivery()) { acquired.Set(); }
		}) { IsBackground = true }.Start();
		return acquired.Wait(TestTimeouts.WaitFor);
	}

	[Fact]
	public void releasing_on_the_holding_thread_frees_delivery() {
		var hold = _listener.HoldDelivery();
		hold.Dispose();

		Assert.True(DeliveryIsFree());
	}

	[Fact]
	public void releasing_twice_on_the_holding_thread_is_a_no_op() {
		var hold = _listener.HoldDelivery();
		hold.Dispose();
		hold.Dispose();

		Assert.True(DeliveryIsFree());
	}

	/// <summary>
	/// The failure this guards is silent: a swallowed release reports success and leaves the listener
	/// holding its delivery lock for good, with nothing downstream able to tell.
	/// </summary>
	[Fact]
	public void releasing_on_another_thread_throws_and_keeps_the_hold() {
		var hold = _listener.HoldDelivery();
		try {
			Assert.IsType<SynchronizationLockException>(ThrownOffThread(hold.Dispose));

			// Still held, so the failed release did not quietly half-succeed.
			Assert.False(DeliveryIsFree());
		} finally {
			// And still releasable by its owner, so the throw did not spend the handle either.
			hold.Dispose();
		}

		Assert.True(DeliveryIsFree());
	}
}
