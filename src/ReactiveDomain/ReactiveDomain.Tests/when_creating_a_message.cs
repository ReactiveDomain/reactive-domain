using System;
using ReactiveDomain.Tests.Helpers;
using Xunit;

namespace ReactiveDomain.Tests
{
    // ReSharper disable InconsistentNaming
    public class when_creating_a_message
    {
        [Fact]
        public void new_message_with_default_constructor_should_have_an_id()
        {
            var msg1 = new TestMessage();
            Assert.NotEqual(Guid.Empty, msg1.MsgId);
        }
        [Fact]
        public void new_message_with_default_constructor_should_have_new_id()
        {
            var msg1 = new TestMessage();
            Assert.NotEqual(Guid.Empty, msg1.MsgId);
            var msg2 = new TestMessage();
            Assert.NotEqual(Guid.Empty, msg2.MsgId);
            Assert.NotEqual(msg1.MsgId, msg2.MsgId);
        }
    }
    // ReSharper restore InconsistentNaming
}
