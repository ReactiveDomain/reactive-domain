using System;
using ReactiveDomain.Legacy.CommonDomain;

namespace ReactiveDomain.Foundation.Tests.EventStore
{
    public class TestWoftamAggregate : AggregateBase
    {
        public TestWoftamAggregate(Guid aggregateId) : this()
        {
            RaiseEvent(new TestWoftamAggregateCreated(aggregateId));
        }

        private TestWoftamAggregate()
        {
            Register<TestWoftamAggregateCreated>(e => Id = e.AggregateId);
            Register<WoftamEvent>(e => AppliedEventCount++);
        }

        public int AppliedEventCount { get; private set; }

        public void ProduceEvents(int count)
        {
            for (int i = 0; i < count; i++)
                RaiseEvent(new WoftamEvent("Woftam1-" + i, "Woftam2-" + i));
        }
    }
}