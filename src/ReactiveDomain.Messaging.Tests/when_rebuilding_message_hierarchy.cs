using System.Reflection;
using ReactiveDomain.Messaging.Bus;
using Xunit;

namespace ReactiveDomain.Messaging.Tests
{
    // ReSharper disable InconsistentNaming
    public class when_rebuilding_message_hierarchy
    {
        sealed class TestMsg : Message { }

        [Fact]
        public void in_memory_bus_remembers_handlers()
        {
            var method = typeof(MessageHierarchy).GetMethod("RebuildMessageTree", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(method);

            var bus = InMemoryBus.CreateTest();

            var fired = false;
            bus.Subscribe(new AdHocHandler<TestMsg>(m => fired = true));
            
            bus.Publish(new TestMsg());
            Assert.True(fired);

            // ReSharper disable once PossibleNullReferenceException
            method.Invoke(null, new object[0]);

            fired = false;
            bus.Publish(new TestMsg());
            Assert.True(fired);
        }
    }
    // ReSharper restore InconsistentNaming
}
