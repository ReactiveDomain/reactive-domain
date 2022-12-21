using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReactiveDomain.Messaging;

namespace ReactiveDomain
{
	public class SerializationRegistry
	{
		private readonly JsonSerializerSettings _serializerSettings;
		private readonly Dictionary<Type, Func<Message, StorableMessage>> _messageSerializers;

		private readonly Dictionary<Type, Dictionary<string, Func<SerializedMessage, IEnumerable<Message>>>>
			_messageDeserializers;

		private readonly Dictionary<Type, Func<Snapshot, StorableMessage>> _snapshotSerializers;
		private readonly Dictionary<string, Func<SerializedMessage, Snapshot>> _snapshotDeserializers;
		private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);
		private readonly Func<Message, byte[]> _metadataSerializer;

		private readonly Func<SerializedMessage, Metadata> _metadataDeserializer;
		private readonly Func<Snapshot, byte[]> _snapshotMetadataSerializer;

		public SerializationRegistry(JsonSerializerSettings serializerSettings = null)
		{
			_serializerSettings = serializerSettings;
			_metadataSerializer = SerializeMetadata(serializerSettings);
			_metadataDeserializer = DeserializeMetadata(serializerSettings);
			_snapshotMetadataSerializer = SerializeSnapshotMetadata(serializerSettings);
			_messageSerializers = new Dictionary<Type, Func<Message, StorableMessage>>();
			_messageDeserializers =
				new Dictionary<Type, Dictionary<string, Func<SerializedMessage, IEnumerable<Message>>>>();
			_snapshotSerializers = new Dictionary<Type, Func<Snapshot, StorableMessage>>();
			_snapshotDeserializers = new Dictionary<string, Func<SerializedMessage, Snapshot>>();
		}

		static Func<Message, byte[]> SerializeMetadata(JsonSerializerSettings settings)
		{
			var serializer = JsonSerializer.Create(settings);

			byte[] Serialize(Message m)
			{
				return SerializeMetadata(m, serializer);
			}

			return Serialize;
		}

		static Func<Snapshot, byte[]> SerializeSnapshotMetadata(JsonSerializerSettings settings)
		{
			var serializer = JsonSerializer.Create(settings);

			byte[] Serialize(Snapshot m)
			{
				return SerializeMetadata(m, serializer);
			}

			return Serialize;
		}

		private static byte[] SerializeMetadata<T>(T mds, JsonSerializer serializer) where T : IMetadataSource
		{
			var metadata = mds.ReadMetadata();
			if (metadata == null)
				return null;

			var data = metadata.GetData();
			var json = JObject.FromObject(data, serializer);
			if (metadata.Root != null && data.Count > 0)
			{
				metadata.Root.Merge(json, new JsonMergeSettings()
				{
					MergeArrayHandling = MergeArrayHandling.Replace
				});
				json = metadata.Root;
			}

			return Utf8NoBom.GetBytes(json.ToString());
		}

		static Func<SerializedMessage, Metadata> DeserializeMetadata(JsonSerializerSettings settings)
		{
			var serializer = JsonSerializer.Create(settings);

			Metadata Deserialize(SerializedMessage msg)
			{
				if (msg.Metadata == null)
					return null;
				var root = JObject.Parse(Utf8NoBom.GetString(msg.Metadata));
				return new Metadata(root, serializer);
			}

			return Deserialize;
		}

		public void RegisterMessageSerializer(Type type, string name, int version, Func<Message, JObject> serializer)
		{
			StorableMessage Serialize(Message msg)
			{
				var id = Guid.NewGuid();
				string formattedName = $"{name}.{version}";
				return new StorableMessage(id, formattedName,
					Utf8NoBom.GetBytes(serializer(msg).ToString()),
					_metadataSerializer(msg));
			}

			if (_messageSerializers.ContainsKey(type))
			{
				throw new InvalidOperationException($"MessageSerializer already registered for {type.FullName}");
			}

			_messageSerializers[type] = Serialize;
		}

		public void RegisterMessageDeserializer(Type scope, string name, int version,
			Func<JObject, IEnumerable<Message>> deserializer)
		{
			IReadOnlyList<Message> Deserialize(SerializedMessage serialized)
			{

				var msgs = deserializer(JObject.Parse(Utf8NoBom.GetString(serialized.Data))).ToArray();
				var md = _metadataDeserializer(serialized);

				for (var i = 0; i < msgs.Length; i++)
				{
					InitializeMetadata(msgs[i], md);
				}


				return msgs;
			}

			if (!_messageDeserializers.TryGetValue(scope, out var deserializers))
			{
				_messageDeserializers[scope] =
					deserializers = new Dictionary<string, Func<SerializedMessage, IEnumerable<Message>>>();
			}

			var typeAndVersion = $"{name}.{version}";
			if (deserializers.ContainsKey(typeAndVersion))
			{
				throw new InvalidOperationException();
			}

			deserializers[typeAndVersion] = Deserialize;
		}

		public void RegisterSnapshotSerializer(Type type, string name, int version, Func<Snapshot, JObject> serializer)
		{
			StorableMessage Serialize(Snapshot snapshot)
			{
				var serialized = serializer(snapshot);
				var data = Utf8NoBom.GetBytes(serialized.ToString());
				return new StorableMessage(Guid.NewGuid(), $"{name}.{version}",
					data, _snapshotMetadataSerializer(snapshot));
			}

			_snapshotSerializers.Add(type, Serialize);
		}

		public void RegisterSnapshotDeserializer(string name, int version, Func<JObject, Snapshot> deserializer)
		{
			Snapshot Deserialize(SerializedMessage serialized)
			{
				var json = JObject.Parse(Utf8NoBom.GetString(serialized.Data));
				var snapshot = deserializer(json);
				var md = _metadataDeserializer(serialized);
				InitializeMetadata(snapshot, md);
				return snapshot;
			}

			_snapshotDeserializers.Add($"{name}.{version}", Deserialize);
		}

		static void InitializeMetadata<T>(T instance, Metadata md) where T : IMetadataSource
		{
			if (md == null) return;
			instance.Initialize(md);
		}

		public Serialization Build()
		{
			return new Serialization(
				MessageSerializers(),
				SnapshotSerializers(),
				_metadataDeserializer);
		}

		private SerializationPair<ISnapshotSerializer, ISnapshotDeserializer> SnapshotSerializers()
		{
			return new SerializationPair<ISnapshotSerializer, ISnapshotDeserializer>(new SnapshotSerializer(_snapshotSerializers), new SnapshotDeserializer(_snapshotDeserializers));
		}

		private SerializationPair<IMessageSerializer, IMessageDeserializer> MessageSerializers()
		{
			return new SerializationPair<IMessageSerializer, IMessageDeserializer>(
				new MessageSerializer(_messageSerializers), new MessageDeserializer(_messageDeserializers));
		}

		class MessageSerializer : IMessageSerializer
		{
			private readonly IReadOnlyDictionary<Type, Func<Message, StorableMessage>> _serializers;

			public MessageSerializer(IReadOnlyDictionary<Type, Func<Message, StorableMessage>> serializers)
			{
				_serializers = serializers;
			}

			public StorableMessage Serialize(Message msg)
			{
				if (!_serializers.TryGetValue(msg.GetType(), out var serializer))
					throw new InvalidOperationException();
				return serializer(msg);
			}
		}

		class MessageDeserializer : IMessageDeserializer
		{
			private readonly
				IReadOnlyDictionary<Type, Dictionary<string, Func<SerializedMessage, IEnumerable<Message>>>>
				_deserializers;

			public MessageDeserializer(
				IReadOnlyDictionary<Type, Dictionary<string, Func<SerializedMessage, IEnumerable<Message>>>>
					deserializers)
			{
				_deserializers = deserializers;
			}

			public Func<SerializedMessage, IEnumerable<Message>> CreateDeserializer<T>()
			{
				if (!_deserializers.TryGetValue(typeof(T), out var deserializers))
				{
					deserializers = _deserializers[typeof(object)];
				}

				IEnumerable<Message> ScopedDeserializer(SerializedMessage msg)
				{
					if (!deserializers.TryGetValue(msg.Name, out var deserializer))
						return Enumerable.Empty<Message>();
					return deserializer(msg);
				}

				return ScopedDeserializer;
			}
		}

		class SnapshotSerializer : ISnapshotSerializer
		{
			private readonly IReadOnlyDictionary<Type, Func<Snapshot, StorableMessage>> _serializers;

			public SnapshotSerializer(IReadOnlyDictionary<Type, Func<Snapshot, StorableMessage>> serializers)
			{
				_serializers = serializers;
			}

			public StorableMessage Serialize(Snapshot snapshot)
			{
				if(!_serializers.TryGetValue(snapshot.GetType(), out var serializer))
					throw new InvalidOperationException($"No serializer found for snapshot of type {snapshot.GetType().FullName}");
				return serializer(snapshot);
			}
		}

		class SnapshotDeserializer : ISnapshotDeserializer
		{
			private readonly IReadOnlyDictionary<string, Func<SerializedMessage, Snapshot>> _deserializers;

			public SnapshotDeserializer(IReadOnlyDictionary<string, Func<SerializedMessage, Snapshot>> deserializers)
			{
				_deserializers = deserializers;
			}

			public T Deserialize<T>(SerializedMessage message) where T : Snapshot
			{
				if(!_deserializers.TryGetValue(message.Name, out var deserializer))
					throw new InvalidOperationException($"Unable to find deserializer for {message.Name}");
				return (T)deserializer(message);
			}
		}
}
}