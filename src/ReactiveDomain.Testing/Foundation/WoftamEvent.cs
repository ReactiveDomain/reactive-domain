using System;
using ReactiveDomain.Messaging;

namespace ReactiveDomain.Testing
{
    public class WoftamEvent : IEvent
    {
        public Guid MsgId { get; private set; }
        public string Property1 { get; private set; }
        public string Property2 { get; private set; }

        public WoftamEvent(string property1, string property2)
        {
            MsgId = Guid.NewGuid();
            Property1 = property1;
            Property2 = property2;
        }
    }
}