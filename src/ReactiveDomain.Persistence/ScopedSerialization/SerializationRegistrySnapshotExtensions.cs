using Newtonsoft.Json.Linq;

namespace ReactiveDomain
{
	public static class SerializationRegistrySnapshotExtensions
	{
		public static SerializationRegistry Default<T>(this SerializationRegistry registry) where T : Snapshot
		{
			registry.RegisterSnapshotSerializer(typeof(T), typeof(T).Name, 1, JObject.FromObject);
			registry.RegisterSnapshotDeserializer(typeof(T).Name, 1, x =>  x.ToObject<T>() );
			return registry;
		}
	}
}