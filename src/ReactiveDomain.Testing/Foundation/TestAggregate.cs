using System;

namespace ReactiveDomain.Testing
{
    public class TestAggregate : EventDrivenStateMachine
    {
        private uint _amount;

        public TestAggregate(Guid id)
            : this()
        {
            if (id == Guid.Empty) throw new ArgumentOutOfRangeException("id", id, "ID cannot be Guid.Empty");
            Raise(new TestAggregateMessages.NewAggregate(id));
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
            if (amount < 1) throw new ArgumentOutOfRangeException(nameof(amount), amount, "Amount must be greater than 0.");
            Raise(new TestAggregateMessages.Increment(Id, (uint)amount));
        }

        private void RegisterEvents()
        {
            Register<TestAggregateMessages.NewAggregate>(e => Id = e.AggregateId);
            Register<TestAggregateMessages.Increment>(e => { _amount += e.Amount; });

        }
    }
}

