using Newtonsoft.Json.Linq;
using ReactiveDomain.Messaging;

namespace ReactiveDomain
{
	public static class SerializationRegistryMessageExtensions
	{
		public static SerializationRegistry Default<T>(this SerializationRegistry registry) where T : Message
		{
			registry.RegisterMessageSerializer(typeof(T), typeof(T).Name, 1, JObject.FromObject);
			registry.RegisterMessageDeserializer(typeof(object), typeof(T).Name, 1, x => new Message[] { x.ToObject<T>() });
			return registry;
		}
		
	}
}