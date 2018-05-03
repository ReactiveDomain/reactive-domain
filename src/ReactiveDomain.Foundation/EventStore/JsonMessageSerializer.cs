using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReactiveDomain.Messaging;

// ReSharper disable once CheckNamespace
namespace ReactiveDomain.Foundation {
    public class JsonMessageSerializer : IEventSerializer {
        private static readonly JsonSerializerSettings SerializerSettings;
        public const string EventClrQualifiedTypeHeader = "EventClrQualifiedTypeName";
        public const string EventClrTypeHeader = "EventClrTypeName";

        static JsonMessageSerializer() {
            SerializerSettings = new JsonSerializerSettings {
                TypeNameHandling = TypeNameHandling.Auto,
                Converters = new JsonConverter[]
                {
                    new SourceId.SourceIdGuidConverter(),
                    new CorrelationId.CorrelationIdGuidConverter()
                }
            };
        }
        public EventData Serialize(object @event, IDictionary<string, object> headers = null) {

            if (headers == null) {
                headers = new Dictionary<string, object>();
            }

            try {
                headers.Add(EventClrTypeHeader, @event.GetType().Name);
                headers.Add(EventClrQualifiedTypeHeader, @event.GetType().AssemblyQualifiedName);
            }
            catch (Exception e) {
                var msg = e.Message;
                
            }
            var metadata = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(headers, SerializerSettings));
            var dString = JsonConvert.SerializeObject(@event, SerializerSettings);
            var data = Encoding.UTF8.GetBytes(dString);
            var typeName = @event.GetType().Name;

            return new EventData(Guid.NewGuid(), typeName, true, data, metadata);
        }

        public object Deserialize(IEventData @event) {

            var eventClrTypeName = JObject.Parse(
                                Encoding.UTF8.GetString(@event.Metadata)).Property(EventClrQualifiedTypeHeader).Value; // todo: fallback to using type name optionally
            return JsonConvert.DeserializeObject(
                                Encoding.UTF8.GetString(@event.Data),
                                Type.GetType((string)eventClrTypeName),
                                SerializerSettings);
        }


    }
}
