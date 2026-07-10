namespace ReactiveDomain;

public class SerializationPair<TSerializer, TDeserializer>(TSerializer serializer, TDeserializer deserializer) {
	public readonly TSerializer Serializer = serializer;
	public readonly TDeserializer Deserializer = deserializer;
}
