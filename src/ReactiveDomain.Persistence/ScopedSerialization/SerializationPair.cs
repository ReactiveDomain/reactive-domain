namespace ReactiveDomain
{
	public class SerializationPair<TSerializer, TDeserializer>
	{
		public readonly TSerializer Serializer;
		public readonly TDeserializer Deserializer;

		public SerializationPair(TSerializer serializer, TDeserializer deserializer)
		{
			Serializer = serializer;
			Deserializer = deserializer;
		}
	}
}