using ReactiveDomain.EventStore;
using Xunit;

namespace ReactiveDomain.Testing.Tests.StreamStore;

/// <summary>
/// Pins establishment wait outcomes used by <see cref="GrpcConnectionWrapper"/> subscriptions.
/// </summary>
public sealed class GrpcConnectionWrapperEstablishTests {
	[Fact]
	public void await_established_returns_established_when_task_completes() {
		var tcs = new TaskCompletionSource();
		tcs.SetResult();
		Assert.Equal(
			GrpcConnectionWrapper.EstablishOutcome.Established,
			GrpcConnectionWrapper.AwaitEstablished(tcs.Task, TimeSpan.FromSeconds(1)));
	}

	[Fact]
	public void await_established_returns_timed_out_when_task_does_not_complete() {
		var tcs = new TaskCompletionSource();
		Assert.Equal(
			GrpcConnectionWrapper.EstablishOutcome.TimedOut,
			GrpcConnectionWrapper.AwaitEstablished(tcs.Task, TimeSpan.FromMilliseconds(50)));
	}

	[Fact]
	public void await_established_returns_faulted_when_task_faults() {
		var tcs = new TaskCompletionSource();
		tcs.SetException(new InvalidOperationException("boom"));
		Assert.Equal(
			GrpcConnectionWrapper.EstablishOutcome.Faulted,
			GrpcConnectionWrapper.AwaitEstablished(tcs.Task, TimeSpan.FromSeconds(1)));
	}

	[Fact]
	public void ensure_established_returns_when_task_completes() {
		using var cts = new CancellationTokenSource();
		var tcs = new TaskCompletionSource();
		tcs.SetResult();
		GrpcConnectionWrapper.EnsureEstablished(tcs.Task, cts, TimeSpan.FromSeconds(1));
		Assert.False(cts.IsCancellationRequested);
	}

	[Fact]
	public void ensure_established_throws_timeout_and_disposes_cts_on_timed_out() {
		var cts = new CancellationTokenSource();
		var tcs = new TaskCompletionSource();
		Assert.Throws<TimeoutException>(() =>
			GrpcConnectionWrapper.EnsureEstablished(tcs.Task, cts, TimeSpan.FromMilliseconds(50)));
		Assert.Throws<ObjectDisposedException>(() => cts.Token);
	}

	[Fact]
	public void ensure_established_throws_and_disposes_cts_on_faulted() {
		var cts = new CancellationTokenSource();
		var tcs = new TaskCompletionSource();
		tcs.SetException(new InvalidOperationException("boom"));
		var ex = Assert.Throws<InvalidOperationException>(() =>
			GrpcConnectionWrapper.EnsureEstablished(tcs.Task, cts, TimeSpan.FromSeconds(1)));
		Assert.Equal("Subscription failed to establish.", ex.Message);
		Assert.IsType<InvalidOperationException>(ex.InnerException);
		Assert.Equal("boom", ex.InnerException!.Message);
		Assert.Throws<ObjectDisposedException>(() => cts.Token);
	}
}
