using System;
using Newtonsoft.Json;

namespace ReactiveDomain.Messaging
{

    public abstract class Message : IMessage
    {
        [JsonProperty(Required = Required.Always)]
        public Guid MsgId { get; private set; }

        protected Message()
        {
            MsgId = Guid.NewGuid();
        }
    }
}
