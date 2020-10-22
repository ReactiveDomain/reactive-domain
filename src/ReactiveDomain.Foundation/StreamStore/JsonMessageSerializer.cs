using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

// ReSharper disable once CheckNamespace
namespace ReactiveDomain.Foundation
{
    public class JsonMessageSerializer : IEventSerializer
    {

        public static readonly JsonSerializerSettings StandardSerializerSettings;
        public JsonSerializerSettings SerializerSettings
        {
            get => _serializerSettings ?? StandardSerializerSettings;
            set => _serializerSettings = value;
        }
        private JsonSerializerSettings _serializerSettings;
        public string EventClrQualifiedTypeHeader = "EventClrQualifiedTypeName";
        public string EventClrTypeHeader = "EventClrTypeName";
        public bool FullyQualify { get; set; }
        public Assembly AssemblyOverride { get; set; }
        public bool ThrowOnTypeNotFound { get; set; }

        static JsonMessageSerializer()
        {
            var contractResolver = new DefaultContractResolver();
#pragma warning disable 618
            contractResolver.DefaultMembersSearchFlags |= BindingFlags.NonPublic;
#pragma warning restore 618
            StandardSerializerSettings = new JsonSerializerSettings()
            {
                ContractResolver = contractResolver,
                TypeNameHandling = TypeNameHandling.Auto,
                DateFormatHandling = DateFormatHandling.IsoDateFormat,
                NullValueHandling = NullValueHandling.Ignore,
                DefaultValueHandling = DefaultValueHandling.Ignore,
                MissingMemberHandling = MissingMemberHandling.Ignore,
                Converters = new JsonConverter[] { new StringEnumConverter() }
            };           
        }

        /// <summary>
        /// Creates an default instance of the JsonSerializer for serializing and Deserializing Events from
        /// streams in EventStore. consumers are urged to create dedicated serializers that implement IEventSerializer
        /// for any custom needs, such as seamless event upgrades
        /// </summary>
        public JsonMessageSerializer(JsonMessageSerializerSettings messageSerializerSettings = null)
        {
            if (messageSerializerSettings == null) { messageSerializerSettings = new JsonMessageSerializerSettings(); }
            FullyQualify = messageSerializerSettings.FullyQualify;
            AssemblyOverride = messageSerializerSettings.AssemblyOverride;
            ThrowOnTypeNotFound = messageSerializerSettings.ThrowOnTypeNotFound;
        }
        public EventData Serialize(object @event, IDictionary<string, object> headers = null)
        {
            if (headers == null) { headers = new Dictionary<string, object>(); }


            if (!headers.ContainsKey(EventClrTypeHeader))
            {
                headers.Add(EventClrTypeHeader, @event.GetType().Name);
            }

            if (!headers.ContainsKey(EventClrQualifiedTypeHeader))
            {
                string qualifiedName;
                if (FullyQualify) { qualifiedName = @event.GetType().AssemblyQualifiedName; }
                else { qualifiedName = $"{@event.GetType().FullName},{@event.GetType().Assembly.GetName()}"; }
                headers.Add(EventClrQualifiedTypeHeader, qualifiedName);
            }

            var metadata = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(headers, SerializerSettings));
            var dString = JsonConvert.SerializeObject(@event, SerializerSettings);
            var data = Encoding.UTF8.GetBytes(dString);
            var typeName = @event.GetType().Name;

            return new EventData(Guid.NewGuid(), typeName, true, data, metadata);
        }

        public object Deserialize(IEventData @event)
        {
            var clrQualifiedName = (string)JObject.Parse(Encoding.UTF8.GetString(@event.Metadata))
                                                    .Property(EventClrQualifiedTypeHeader).Value;
            return Deserialize(@event, clrQualifiedName);
        }

        public object Deserialize(IEventData @event, string fullyQualifiedName)
        {
            return Deserialize(@event, FindType(fullyQualifiedName));
        }
        public Type FindType(string fullyQualifiedName)
        {
            if (string.IsNullOrWhiteSpace(fullyQualifiedName))
            {
                throw new ArgumentNullException(nameof(fullyQualifiedName), $"{fullyQualifiedName} cannot be null, empty, or whitespace");
            }
            var nameParts = fullyQualifiedName.Split(',');
            var fullName = nameParts[0];
            var assemblyName = nameParts[1];
            Type targetType;
            if (AssemblyOverride != null)
            {
                targetType = AssemblyOverride.GetType(fullName);
                if (ThrowOnTypeNotFound && targetType == null)
                {
                    throw new InvalidOperationException($"Type not found for {fullName} in overridden assembly {AssemblyOverride.FullName}");
                }
                return targetType;
            }
            if (FullyQualify)
            {
                targetType = Type.GetType(fullyQualifiedName);
                if (ThrowOnTypeNotFound && targetType == null)
                {
                    throw new InvalidOperationException($"Type not found for {fullyQualifiedName}");
                }
                return targetType;
            }

            var partialQualifiedName = $"{fullName},{assemblyName}";
            targetType = Type.GetType(partialQualifiedName);
            if (ThrowOnTypeNotFound && targetType == null)
            {
                throw new InvalidOperationException($"Type not found for {partialQualifiedName}");
            }
            return targetType;
        }

        public object Deserialize(IEventData @event, Type type)
        {
            return JsonConvert.DeserializeObject(
                Encoding.UTF8.GetString(@event.Data),
                type,
                SerializerSettings);
        }
        public TEvent Deserialize<TEvent>(IEventData @event)
        {
            return (TEvent)JsonConvert.DeserializeObject(
                Encoding.UTF8.GetString(@event.Data),
                typeof(TEvent),
                SerializerSettings);
        }

    }
}
