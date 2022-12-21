// ReSharper disable MemberCanBePrivate.Global

using System;
using System.Reflection;
using System.Xml;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using ReactiveDomain.Util;
using Formatting = Newtonsoft.Json.Formatting;

namespace ReactiveDomain
{
    public static class Json
    {
        public class MetadataIgnoringResolver : DefaultContractResolver
        {
            protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
            {
                var property = base.CreateProperty(member, memberSerialization);

                if (property.PropertyName == "_metadata")
                {
                    property.ShouldSerialize = _ => false;
                    property.Ignored = true;
                }

                return property;
            }
        }

        private static IContractResolver GetContractResolver()
        {
            //Metadata is deserialized lazily with dedicated deserializers 
            var resolver = new MetadataIgnoringResolver();
            resolver.DefaultMembersSearchFlags |= BindingFlags.NonPublic;
            return resolver;
        }

        /// <summary>
        /// The default JSON serializer settings.
        /// </summary>
        public static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            ContractResolver = GetContractResolver(),
            DateFormatHandling = DateFormatHandling.IsoDateFormat,
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Ignore,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            TypeNameHandling = TypeNameHandling.Auto,
            Converters = new JsonConverter[] { new StringEnumConverter() }
        };

        /// <summary>
        /// The default JSON serializer settings for logging.
        /// </summary>
        public static readonly JsonSerializerSettings JsonLoggingSettings = new JsonSerializerSettings
        {
            ContractResolver = GetContractResolver(),
            DateFormatHandling = DateFormatHandling.IsoDateFormat,
            NullValueHandling = NullValueHandling.Include,
            DefaultValueHandling = DefaultValueHandling.Include,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            TypeNameHandling = TypeNameHandling.Auto,
            Converters = new JsonConverter[] { new StringEnumConverter() }
        };

        /// <summary>
        /// Serialized an object to JSON that is then encoded as a byte array. Uses the default JSON serializer settings.
        /// </summary>
        /// <param name="source">The object to serialize.</param>
        /// <returns>The serialized object as a byte array.</returns>
        public static byte[] ToJsonBytes(this object source)
        {
            string inString = JsonConvert.SerializeObject(source, Formatting.Indented, JsonSettings);
            return Helper.UTF8NoBom.GetBytes(inString);
        }

        /// <summary>
        /// Serialized an object to JSON. Uses the default JSON serializer settings.
        /// </summary>
        /// <param name="source">The object to serialize.</param>
        /// <returns>The serialized object.</returns>
        public static string ToJson(this object source)
        {
            string inString = JsonConvert.SerializeObject(source, Formatting.Indented, JsonSettings);
            return inString;
        }

        /// <summary>
        /// Serialized an object to JSON. Uses the default JSON serializer settings for logging.
        /// </summary>
        /// <param name="source">The object to serialize.</param>
        /// <returns>The serialized object.</returns>
        public static string ToLogJson(this object source)
        {
            string inString = JsonConvert.SerializeObject(source, Formatting.Indented, JsonLoggingSettings);
            return inString;
        }

        /// <summary>
        /// Serialized an object to JSON using the canonical serializer.
        /// </summary>
        /// <param name="source">The object to serialize.</param>
        /// <returns>The serialized object.</returns>
        public static string ToCanonicalJson(this object source)
        {
            string inString = JsonConvert.SerializeObject(source);
            return inString;
        }

        /// <summary>
        /// Deserializes a JSON string to an object. Uses the default JSON serializer settings.
        /// </summary>
        /// <typeparam name="T">The type of object in the serialized string</typeparam>
        /// <param name="json">The serialized string.</param>
        /// <returns>An object of the specified type.</returns>
        public static T ParseJson<T>(this string json)
        {
            var result = JsonConvert.DeserializeObject<T>(json, JsonSettings);
            return result;
        }

        /// <summary>
        /// Deserializes a byte array that encodes a JSON string. Uses the default JSON serializer settings.
        /// </summary>
        /// <typeparam name="T">The type of object in the serialized string</typeparam>
        /// <param name="json">The serialized byte array.</param>
        /// <returns>An object of the specified type.</returns>
        public static T ParseJson<T>(this byte[] json)
        {
            var result = JsonConvert.DeserializeObject<T>(Helper.UTF8NoBom.GetString(json), JsonSettings);
            return result;
        }

        /// <summary>
        /// Deserializes a JSON object to the specified type.
        /// </summary>
        /// <param name="value">The JSON object to deserialize.</param>
        /// <param name="type">The type of the deserialized object.</param>
        /// <param name="settings">The JSON serialization settings to use.</param>
        /// <returns>An object of the specified type.</returns>
        public static object DeserializeObject(JObject value, Type type, JsonSerializerSettings settings)
        {
            JsonSerializer jsonSerializer = JsonSerializer.Create(settings);
            return jsonSerializer.Deserialize(new JTokenReader(value), type);
        }

        /// <summary>
        /// Deserializes a JSON object to the specified type.
        /// </summary>
        /// <param name="value">The JSON object to deserialize.</param>
        /// <param name="type">The type of the deserialized object.</param>
        /// <param name="converters">An array of JSON converters for the types in the JSON object.</param>
        /// <returns>An object of the specified type.</returns>
        public static object DeserializeObject(JObject value, Type type, params JsonConverter[] converters)
        {
            var settings = converters == null || converters.Length <= 0
                                             ? null
                                             : new JsonSerializerSettings { Converters = converters };
            return DeserializeObject(value, type, settings);
        }

        /// <summary>
        /// Converts a JSON object to an <see cref="XmlDocument"/>.
        /// </summary>
        /// <param name="value">The JSON object to convert.</param>
        /// <param name="deserializeRootElementName">The name of the root element to insert when deserializing to XML if the JSON structure has produced multiple root elements.</param>
        /// <param name="writeArrayAttribute">Indicates whether to write the Json.NET array attribute.
        /// This helps preserve arrays when converting the written XML back to JSON.</param>
        /// <returns></returns>
        public static XmlDocument ToXmlDocument(this JObject value, string deserializeRootElementName, bool writeArrayAttribute)
        {
            return (XmlDocument)DeserializeObject(value, typeof(XmlDocument), new JsonConverter[]
            {
                new XmlNodeConverter
                {
                    DeserializeRootElementName = deserializeRootElementName,
                    WriteArrayAttribute = writeArrayAttribute
                }
            });
        }
    }
}
