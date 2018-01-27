using System;
using ReactiveDomain.Messaging.Util;
using Xunit;

// ReSharper disable InconsistentNaming

namespace ReactiveDomain.Messaging.Tests
{
    public class when_ensuring_values
    {
        [Fact]
        public void can_ensure_not_default_datetime()
        {
            Ensure.NotDefault(DateTime.Now, "DateTime");
            Assert.Throws<ArgumentException>(() => Ensure.NotDefault(new DateTime(), "DateTime"));
        }
    }
}
