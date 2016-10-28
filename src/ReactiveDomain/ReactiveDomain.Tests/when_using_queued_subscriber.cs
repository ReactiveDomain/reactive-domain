using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ReactiveDomain.Tests.Helpers;
using ReactiveDomain.Tests.Specifications;

namespace ReactiveDomain.Tests
{
    // ReSharper disable InconsistentNaming
    public abstract class when_using_queued_subscriber : CommandBusSpecification
    {
        protected TestMessageSubscriber _messageSubscriber;

        protected override void Given()
        {
            _messageSubscriber = new TestMessageSubscriber(Bus);
        }

    }
}
