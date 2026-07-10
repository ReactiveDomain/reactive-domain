namespace ReactiveDomain.Messaging.Bus;

public interface ITimeSource {
	TimePosition Now();
	void WaitFor(TimePosition position, ManualResetEventSlim cancel);
}
public class TimeSource : ITimeSource {
	public static readonly TimeSource System = new();
	public TimePosition Now() {
		return new TimePosition(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
	}
	public void WaitFor(TimePosition position, ManualResetEventSlim cancel) {
		cancel.Wait(Now().DistanceUntil(position));
	}
}
