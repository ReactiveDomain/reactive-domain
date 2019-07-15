using System;
using System.Threading;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;
using Xunit;

namespace ReactiveDomain.Messaging.Tests {
    public class LaterServiceTests {
        [Fact]
        public void CanDelayPublish() {
            var timeSource = new TestTimeSource();
            long msgCount = 0;

            using (var delay = 
                new LaterService(
                    // ReSharper disable once AccessToModifiedClosure
                    new TestPublisher(_ => Interlocked.Increment(ref msgCount)), timeSource)) {
                
                delay.Start();

                delay.Handle(new DelaySendEnvelope(timeSource, TimeSpan.FromMilliseconds(50), new TestMessage()));
                timeSource.AdvanceTime(49);
                SpinWait.SpinUntil(() => false, TimeSpan.FromMilliseconds(10));
                Assert.True(Interlocked.Read(ref msgCount) == 0);
                timeSource.AdvanceTime(1);
                AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref msgCount) == 1);
            }
        }
        [Fact]
        public void ExpiredEnvelopesPublish() {
            var timeSource = new TestTimeSource();
            long msgCount = 0;
            using (var delay =
                new LaterService(
                    new TestPublisher(_ => Interlocked.Increment(ref msgCount)), timeSource)) {

                delay.Start();
                var sendTime = timeSource.Now() + TimeSpan.FromMilliseconds(50);
                timeSource.AdvanceTime(100);
                delay.Handle(new DelaySendEnvelope(sendTime, new TestMessage()));
                AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref msgCount) == 1);
            }
        }


        [Fact]
        public void EnvelopesPublishInSequence() {
            var timeSource = new TestTimeSource();
            long msgCount = 0;
            long messageNumber = -1;
            using (var delay =
                new LaterService(
                    new TestPublisher(msg => {
                        Interlocked.Increment(ref msgCount);
                        var num = ((TestMessage)msg).MessageNumber;
                        // ReSharper disable once AccessToModifiedClosure
                        Interlocked.Exchange(ref messageNumber, num);
                    }),
                                      timeSource)) {

                delay.Start();

                delay.Handle(new DelaySendEnvelope(timeSource, TimeSpan.FromMilliseconds(50), new TestMessage(10)));
                delay.Handle(new DelaySendEnvelope(timeSource, TimeSpan.FromMilliseconds(60), new TestMessage(20)));
                delay.Handle(new DelaySendEnvelope(timeSource, TimeSpan.FromMilliseconds(70), new TestMessage(30)));
                delay.Handle(new DelaySendEnvelope(timeSource, TimeSpan.FromMilliseconds(80), new TestMessage(40)));
                timeSource.AdvanceTime(51);
                AssertEx.IsOrBecomesTrue(()=> Interlocked.Read(ref msgCount) ==1);
                Assert.True(Interlocked.Read(ref messageNumber) == 10);
                
                timeSource.AdvanceTime(10);
                AssertEx.IsOrBecomesTrue(()=> Interlocked.Read(ref msgCount) ==2);
                Assert.True(Interlocked.Read(ref messageNumber) == 20);
                
                timeSource.AdvanceTime(10);
                AssertEx.IsOrBecomesTrue(()=> Interlocked.Read(ref msgCount) ==3);
                Assert.True(Interlocked.Read(ref messageNumber) == 30);
                
                timeSource.AdvanceTime(10);
                AssertEx.IsOrBecomesTrue(()=> Interlocked.Read(ref msgCount) ==4);
                Assert.True(Interlocked.Read(ref messageNumber) == 40);
                
            }
        }


        [Fact]
        public void EnvelopesCanPublishAtTheSameTime() {
            var timeSource = new TestTimeSource();
            long msgCount = 0;

            using (var delay =
                new LaterService(
                    new TestPublisher(_ => Interlocked.Increment(ref msgCount)),
                    timeSource)) {

                delay.Start();

                delay.Handle(new DelaySendEnvelope(timeSource, TimeSpan.FromMilliseconds(50), new TestMessage(1)));
                delay.Handle(new DelaySendEnvelope(timeSource, TimeSpan.FromMilliseconds(50), new TestMessage(2)));
                delay.Handle(new DelaySendEnvelope(timeSource, TimeSpan.FromMilliseconds(50), new TestMessage(3)));
                delay.Handle(new DelaySendEnvelope(timeSource, TimeSpan.FromMilliseconds(50), new TestMessage(4)));
                timeSource.AdvanceTime(51);
                AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref msgCount) == 4);
            }
        }
        [Fact]
        [Trait("Category", "LongRunning")]
        public void DelayPublishLoadTest() {
            var timeSource = new TestTimeSource();
            var iterations = 10000;
            var start = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            using (var cd = new CountdownEvent(iterations))
                // ReSharper disable once AccessToDisposedClosure
            using (var delay = new LaterService(new TestPublisher(_ => cd.Signal()), timeSource)) {
                delay.Start();
                for (int i = 0; i < iterations; i++) {
                    if ((i % 100) == 0) timeSource.AdvanceTime(1);
                    delay.Handle(new DelaySendEnvelope(timeSource, TimeSpan.FromMilliseconds(50), new TestMessage()));
                }
                timeSource.AdvanceTime(50);
                cd.Wait(1000);
            }
            Assert.True(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - start <= 15000, $"elapsed {DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - start}");
        }
        [Fact]
        public void DelayPublishLoadTestSystemTime() {
            var iterations = 1000;
            var start = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            using (var cd = new CountdownEvent(iterations))
                // ReSharper disable once AccessToDisposedClosure
            using (var delay = new LaterService(new TestPublisher(_ => cd.Signal()), TimeSource.System)) {
                delay.Start();
                for (int i = 0; i < iterations; i++) {
                    delay.Handle(new DelaySendEnvelope(TimeSource.System, TimeSpan.FromMilliseconds(50), new TestMessage()));
                }
                cd.Wait(1000);
            }
            Assert.True(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - start <= 15000, $"elapsed {DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - start}");
        }

        class TestMessage : IMessage {
            public Guid MsgId { get; private set; }
            public readonly long MessageNumber;

            public TestMessage(
                long messageNumber = 0) {
                MsgId = Guid.NewGuid();
                MessageNumber = messageNumber;
            }
        }


    }
}