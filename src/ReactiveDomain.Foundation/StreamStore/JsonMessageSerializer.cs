using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using ReactiveDomain.Audit;
using ReactiveDomain.Foundation.StreamStore;
using ReactiveDomain.Messaging;

// ReSharper disable once CheckNamespace
namespace ReactiveDomain.Foundation
{
    public class JsonMessageSerializer : IEventSerializer
    {

        public static JsonSerializerSettings StandardSerializerSettings => Json.JsonSettings;
        public JsonSerializerSettings SerializerSettings
        {
            get => _serializerSettings ?? StandardSerializerSettings;
            set => _serializerSettings = value;
        }
        private JsonSerializerSettings _serializerSettings;
        public string EventClrQualifiedTypeHeader = "EventClrQualifiedTypeName";
        public string EventClrTypeHeader = "EventClrTypeName";
        private readonly bool _preferIMetadataSource;

        public bool FullyQualify { get; set; }
        public Assembly AssemblyOverride { get; set; }
        public bool ThrowOnTypeNotFound { get; set; }

        /// <summary>
        /// Creates an default instance of the JsonSerializer for serializing and Deserializing Events from
        /// streams in EventStore. consumers are urged to create dedicated serializers that implement IEventSerializer
        /// for any custom needs, such as seamless event upgrades
        /// </summary>
        public JsonMessageSerializer(JsonMessageSerializerSettings messageSerializerSettings = null, bool PreferIMetadataSource = true)
        {
            if (messageSerializerSettings == null) { messageSerializerSettings = new JsonMessageSerializerSettings(); }
            FullyQualify = messageSerializerSettings.FullyQualify;
            AssemblyOverride = messageSerializerSettings.AssemblyOverride;
            ThrowOnTypeNotFound = messageSerializerSettings.ThrowOnTypeNotFound;
            _preferIMetadataSource = PreferIMetadataSource;
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
            if (@event is IMetadataSource source && _preferIMetadataSource)
            {
                var md = source.ReadMetadata() ?? new Metadata();
                var commonMetadata = new CommonMetadata();
                //commit id
                headers.TryGetValue("CommitId", out var CommitId);
                if (CommitId is Guid commitGuid && commitGuid != Guid.Empty)
                {
                    commonMetadata.CommitId = commitGuid;
                }
                else
                {
                    commonMetadata.CommitId = Guid.NewGuid();
                }
                //aggregate name
                headers.TryGetValue("AggregateClrTypeName", out var AggregateClrTypeName);
                commonMetadata.AggregateName = (string)AggregateClrTypeName ?? "";
                //event name
                if (FullyQualify)
                {
                    commonMetadata.EventName = @event.GetType().AssemblyQualifiedName;
                }
                else
                {
                    commonMetadata.EventName = @event.GetType().FullName;
                }
                commonMetadata.EventAssembly = @event.GetType().Assembly.GetName().ToString();
                md.Write(commonMetadata);
                
                //Audit Data Policy User
                headers.TryGetValue("PolicyUserId", out var userId);
                if (userId is Guid userIdGuid && userIdGuid != Guid.Empty)
                {
                    var auditRecord = new AuditRecord
                    {
                        PolicyUserId = userIdGuid,
                        AggregateName = commonMetadata.AggregateName,
                        EventName = commonMetadata.EventName,
                        CommitId = commonMetadata.CommitId,
                        EventDateUTC = DateTime.UtcNow,
                    };
                    md.Write(auditRecord);
                }
                
                metadata = md.GetData().ToJsonBytes();
            }

            var dString = JsonConvert.SerializeObject(@event, SerializerSettings);
            var data = Encoding.UTF8.GetBytes(dString);
            var typeName = @event.GetType().Name;

            return new EventData(Guid.NewGuid(), typeName, true, data, metadata);
        }

        public object Deserialize(IEventData @event)
        {
            var metaDataObject = JObject.Parse(Encoding.UTF8.GetString(@event.Metadata));
            var clrQualifiedName = (string)metaDataObject.Property(EventClrQualifiedTypeHeader)?.Value;
            if (string.IsNullOrWhiteSpace(clrQualifiedName) || _preferIMetadataSource)
            {
                var md = new Metadata(metaDataObject, JsonSerializer.Create(Json.JsonSettings));
                if (md.TryRead<CommonMetadata>(out var smd))
                {
                    if (!string.IsNullOrEmpty(smd?.EventFullyQualifiedName))
                    {
                        return Deserialize(@event, smd.EventFullyQualifiedName);
                    }
                    if (!string.IsNullOrEmpty(smd?.EventName))
                    {
                        var name = smd.EventName;
                        if (!string.IsNullOrEmpty(smd?.EventAssembly))
                        {
                            name += $",{smd.EventAssembly}";
                        }
                        return Deserialize(@event, name);
                    }
                }
            }
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
            var obj = JsonConvert.DeserializeObject(
                Encoding.UTF8.GetString(@event.Data),
                type,
                SerializerSettings);
            if (obj is IMetadataSource mdObject)
            {
                try
                {
                    mdObject.Initialize(new Metadata(JObject.Parse(Encoding.UTF8.GetString(@event.Metadata)), JsonSerializer.Create(Json.JsonSettings)));
                }
                catch (Exception)
                {
                    //ignore
                }
            }
            return obj;
        }
        public TEvent Deserialize<TEvent>(IEventData @event)
        {
            var evt = (TEvent)JsonConvert.DeserializeObject(
                Encoding.UTF8.GetString(@event.Data),
                typeof(TEvent),
                SerializerSettings);
            if (evt is IMetadataSource mdObject)
            {
                try
                {
                    mdObject.Initialize(new Metadata(JObject.Parse(Encoding.UTF8.GetString(@event.Metadata)), JsonSerializer.Create(Json.JsonSettings)));
                }
                catch (Exception)
                {
                    //ignore
                }
            }
            return evt;
        }

    }
}
