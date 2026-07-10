using ReactiveDomain.Messaging.Bus;

// ReSharper disable once CheckNamespace
namespace ReactiveDomain.Testing;

public class TestTimeSource : ITimeSource {
	private long _virtualTime;
	private readonly List<ManualResetEventSlim> _waiting = [];

	public void AdvanceTime(long msDistance) {
		ArgumentOutOfRangeException.ThrowIfNegative(msDistance);
		_virtualTime += msDistance;

		lock (_waiting) {
			foreach (var mres in _waiting) {
				mres.Set();
			}
		}
	}

	public TimePosition Now() {
		return new TimePosition(_virtualTime);
	}

	public void WaitFor(TimePosition position, ManualResetEventSlim waitEvent) {
		var waitTime = Now().DistanceUntil(position);
		if (waitTime == TimeSpan.Zero) { return; }

		lock (_waiting) { _waiting.Add(waitEvent); }
		waitEvent.Wait(waitTime);
		lock (_waiting) { _waiting.Remove(waitEvent); }
	}
}
