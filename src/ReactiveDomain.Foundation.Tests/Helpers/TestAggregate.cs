using System;
using ReactiveDomain.Legacy.CommonDomain;

namespace ReactiveDomain.Foundation.Tests.Helpers
{
    public class TestAggregate : AggregateBase
    {
        private uint _amount;
        public TestAggregate(Guid id)
            : this()
        {
            if (id == Guid.Empty) throw new ArgumentOutOfRangeException("id", id, "ID cannot be Guid.Empty");
            RaiseEvent(new TestAggregateMessages.NewAggregate(id));
        }


        private TestAggregate()
        {
            RegisterEvents();
        }

        public uint CurrentAmount()
        {
            return _amount;
        }

        public void RaiseBy(int amount)
        {
            if (amount < 1) throw new ArgumentOutOfRangeException("amount", amount, "Amount must be greater than 0.");
            RaiseEvent(new TestAggregateMessages.Increment(Id, (uint)amount));
        }

        private void RegisterEvents()
        {
            Register<TestAggregateMessages.NewAggregate>(e => Id = e.AggregateId);
            Register<TestAggregateMessages.Increment>(e => { _amount += e.Amount; });

        }
    }
}

