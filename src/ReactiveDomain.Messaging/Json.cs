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

namespace ReactiveDomain.Messaging
{
    public static class Json
    {
        private static IContractResolver GetContractresolver() {
            var resolver = new DefaultContractResolver();
            resolver.DefaultMembersSearchFlags |= BindingFlags.NonPublic;
            return resolver;
        }
        public static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            ContractResolver = GetContractresolver(),
            DateFormatHandling = DateFormatHandling.IsoDateFormat,
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Ignore,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            TypeNameHandling = TypeNameHandling.Auto,
            Converters = new JsonConverter[] { new StringEnumConverter() }
        };
        public static readonly JsonSerializerSettings JsonLoggingSettings = new JsonSerializerSettings
        {
            ContractResolver = GetContractresolver(),
            DateFormatHandling = DateFormatHandling.IsoDateFormat,
            NullValueHandling = NullValueHandling.Include,
            DefaultValueHandling = DefaultValueHandling.Include,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            TypeNameHandling = TypeNameHandling.Auto,
            Converters = new JsonConverter[] { new StringEnumConverter() }
        };
        public static byte[] ToJsonBytes(this object source)
        {
            string instring = JsonConvert.SerializeObject(source, Formatting.Indented, JsonSettings);
            return Helper.UTF8NoBom.GetBytes(instring);
        }

        public static string ToJson(this object source)
        {
            string instring = JsonConvert.SerializeObject(source, Formatting.Indented, JsonSettings);
            return instring;
        }
        public static string ToLogJson(this object source)
        {
            string instring = JsonConvert.SerializeObject(source, Formatting.Indented, JsonLoggingSettings);
            return instring;
        }

        public static string ToCanonicalJson(this object source)
        {
            string instring = JsonConvert.SerializeObject(source);
            return instring;
        }

        public static T ParseJson<T>(this string json)
        {
            var result = JsonConvert.DeserializeObject<T>(json, JsonSettings);
            return result;
        }

        public static T ParseJson<T>(this byte[] json)
        {
            var result = JsonConvert.DeserializeObject<T>(Helper.UTF8NoBom.GetString(json), JsonSettings);
            return result;
        }

        public static object DeserializeObject(JObject value, Type type, JsonSerializerSettings settings)
        {
            JsonSerializer jsonSerializer = JsonSerializer.Create(settings);
            return jsonSerializer.Deserialize(new JTokenReader(value), type);
        }

        public static object DeserializeObject(JObject value, Type type, params JsonConverter[] converters)
        {
            var settings = converters == null || converters.Length <= 0
                                             ? null
                                             : new JsonSerializerSettings {Converters = converters};
            return DeserializeObject(value, type, settings);
        }

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
