using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReactiveDomain.Messaging;

namespace ReactiveDomain.Foundation.EventStore
{
    public class JsonSerializer : IEventSerializer
    {
        private static readonly JsonSerializerSettings SerializerSettings;

        static JsonSerializer()
        {
            SerializerSettings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.None,
                Converters = new JsonConverter[]
                {
                    new SourceId.SourceIdGuidConverter(),
                    new CorrelationId.CorrelationIdGuidConverter()
                }
            };
        }

        public byte[] Serialize(object data)
        {
            return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(data, SerializerSettings));
        }

        public byte[] Serialize(IDictionary<string, object> data)
        {
            return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(data, SerializerSettings));
        }

        public object Deserialize(byte[] metadata, byte[] data, string clrQualifiedTypeHeader)
        {
            var settings = new JsonSerializerSettings
            {
                ContractResolver = new ContractResolver(),
                Converters = new JsonConverter[]
                {
                    new SourceId.SourceIdGuidConverter(),
                    new CorrelationId.CorrelationIdGuidConverter()
                }
            };
            var eventClrTypeName = JObject.Parse(Encoding.UTF8.GetString(metadata)).Property(clrQualifiedTypeHeader).Value; // todo: fallback to using type name optionally
            return JsonConvert.DeserializeObject(Encoding.UTF8.GetString(data), Type.GetType((string)eventClrTypeName), settings);
        }
    }
}
