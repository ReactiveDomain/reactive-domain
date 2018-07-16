using System;
using System.Collections.Generic;
using System.Threading;
using ReactiveDomain.Messaging.Bus;

namespace ReactiveDomain.Testing {
    public class TestTimeSource : ITimeSource {
        private long _virtualTime;
        public void AdvanceTime(long msDistance) {
            if (msDistance < 0) { throw new ArgumentOutOfRangeException(); }
            _virtualTime += msDistance;

            lock (_waiting) {
                foreach (var mres in _waiting) {
                    mres.Set();
                }
            }
        }
        private readonly List<ManualResetEventSlim> _waiting = new List<ManualResetEventSlim>();

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
}