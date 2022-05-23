using System;
using Newtonsoft.Json;
using ReactiveDomain.Messaging;

namespace ReactiveDomain.Transport.Serialization
{
    public class SimpleJsonSerializer : IMessageSerializer
    {
        public IMessage DeserializeMessage(string json, Type messageType) => (IMessage)JsonConvert.DeserializeObject(json, messageType, Json.JsonSettings);
        public string SerializeMessage(IMessage message) => JsonConvert.SerializeObject(message, Json.JsonSettings);
    }
}
