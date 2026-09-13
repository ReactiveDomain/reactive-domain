using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;
using Xunit;
// ReSharper disable InconsistentNaming

namespace ReactiveDomain.Messaging.Tests;

public class when_serializing_commands {
	[Fact]
	public void can_serialize_bson_success_commandresponse() {
		var cmd = new TestCommands.TypedResponse(false);
		var nearSide = cmd.Succeed(15);
		TestCommands.TestResponse? farSide;
		var ms = new MemoryStream();
		using (var writer = new BsonDataWriter(ms)) {
			var serializer = JsonSerializer.Create(Json.JsonSettings);
			serializer.Serialize(writer, nearSide);
		}
		var array = ms.ToArray();

		var sb = new StringBuilder();
		var sw = new StringWriter(sb);
		using (var writer = new JsonTextWriter(sw)) {
			var serializer = JsonSerializer.Create(Json.JsonSettings);
			serializer.Serialize(writer, nearSide);
		}

		var ms2 = new MemoryStream(array);

		using (var reader = new BsonDataReader(ms2)) {
			var serializer = JsonSerializer.Create(Json.JsonSettings);
			farSide = serializer.Deserialize<TestCommands.TestResponse>(reader);
		}

		Assert.NotNull(farSide);
		Assert.Equal(nearSide.MsgId, farSide.MsgId);
		Assert.Equal(nearSide.GetType(), farSide.GetType());
		Assert.Equal(nearSide.CorrelationId, farSide.CorrelationId);
		Assert.Equal(nearSide.CommandType, farSide.CommandType);
		Assert.Equal(nearSide.CommandId, farSide.CommandId);
		Assert.Equal(nearSide.CausationId, farSide.CausationId);

		Assert.Equal(nearSide.Data, farSide.Data);
	}
	[Fact]
	public void can_serialize_json_success_commandresponse() {
		var cmd = new TestCommands.TypedResponse(false);
		var nearSide = cmd.Succeed(15);
		TestCommands.TestResponse? farSide;

		var sb = new StringBuilder();
		var sw = new StringWriter(sb);
		using (var writer = new JsonTextWriter(sw)) {
			var serializer = JsonSerializer.Create(Json.JsonSettings);
			serializer.Serialize(writer, nearSide);
		}

		using (var reader = new JsonTextReader(new StringReader(sb.ToString()))) {
			var serializer = JsonSerializer.Create(Json.JsonSettings);
			serializer.SerializationBinder = new DefaultSerializationBinder();
			serializer.ContractResolver = new TestContractResolver();
			farSide = serializer.Deserialize<TestCommands.TestResponse>(reader);
		}

		Assert.NotNull(farSide);
		Assert.Equal(nearSide.MsgId, farSide.MsgId);
		Assert.Equal(nearSide.GetType(), farSide.GetType());
		Assert.Equal(nearSide.CorrelationId, farSide.CorrelationId);
		Assert.Equal(nearSide.CommandType, farSide.CommandType);
		Assert.Equal(nearSide.CommandId, farSide.CommandId);
		Assert.Equal(nearSide.CausationId, farSide.CausationId);

		Assert.Equal(nearSide.Data, farSide.Data);
	}

	[Fact]
	public void success_write_positions_round_trip_through_json_and_bson() {
		var cmd = new TestCommands.Command1();
		var nearSide = (Success)cmd.Succeed(
			new StreamCheckpoint("account-1", 7, new Position(1234, 1200)),
			new StreamCheckpoint("ledger-9", 0));

		var json = JsonConvert.SerializeObject(nearSide, Json.JsonSettings);
		var fromJson = JsonConvert.DeserializeObject<Success>(json, Json.JsonSettings);
		AssertWritePositionsMatch(nearSide, fromJson);

		var ms = new MemoryStream();
		using (var writer = new BsonDataWriter(ms)) {
			JsonSerializer.Create(Json.JsonSettings).Serialize(writer, nearSide);
		}
		using var reader = new BsonDataReader(new MemoryStream(ms.ToArray()));
		var fromBson = JsonSerializer.Create(Json.JsonSettings).Deserialize<Success>(reader);
		AssertWritePositionsMatch(nearSide, fromBson);
	}

	[Fact]
	public void success_without_write_positions_round_trips_as_empty() {
		var nearSide = (Success)new TestCommands.Command1().Succeed();
		Assert.Empty(nearSide.WritePositions);

		var json = JsonConvert.SerializeObject(nearSide, Json.JsonSettings);
		var farSide = JsonConvert.DeserializeObject<Success>(json, Json.JsonSettings);

		Assert.NotNull(farSide);
		Assert.NotNull(farSide.WritePositions);
		Assert.Empty(farSide.WritePositions);
	}

	[Fact]
	public void success_from_a_sender_without_write_positions_reads_as_empty() {
		var nearSide = (Success)new TestCommands.Command1().Succeed();
		var payload = JObject.Parse(JsonConvert.SerializeObject(nearSide, Json.JsonSettings));
		Assert.True(payload.Remove(nameof(Success.WritePositions)));

		var farSide = JsonConvert.DeserializeObject<Success>(payload.ToString(), Json.JsonSettings);

		Assert.NotNull(farSide);
		Assert.Equal(nearSide.CommandId, farSide.CommandId);
		Assert.NotNull(farSide.WritePositions);
		Assert.Empty(farSide.WritePositions);
	}

	private static void AssertWritePositionsMatch(Success nearSide, Success? farSide) {
		Assert.NotNull(farSide);
		Assert.Equal(nearSide.MsgId, farSide.MsgId);
		Assert.Equal(nearSide.CommandId, farSide.CommandId);
		Assert.Equal(nearSide.WritePositions.Count, farSide.WritePositions.Count);
		for (var i = 0; i < nearSide.WritePositions.Count; i++) {
			Assert.Equal(nearSide.WritePositions[i].StreamName, farSide.WritePositions[i].StreamName);
			Assert.Equal(nearSide.WritePositions[i].Version, farSide.WritePositions[i].Version);
			Assert.Equal(nearSide.WritePositions[i].Position, farSide.WritePositions[i].Position);
		}
		Assert.Equal(CheckpointOrder.Equal, StreamCheckpoint.Compare(nearSide.WritePositions, farSide.WritePositions));
	}

	[Fact]
	public void can_serialize_bson_fail_commandresponse() {
		var cmd = new TestCommands.TypedResponse(false);
		var nearSide = cmd.Fail(new CommandException("O_Ops", cmd), 15);
		TestCommands.FailedResponse? farSide;
		var ms = new MemoryStream();
		using (var writer = new BsonDataWriter(ms)) {
			var serializer = JsonSerializer.Create(Json.JsonSettings);
			serializer.Serialize(writer, nearSide);
		}
		var array = ms.ToArray();

		var sb = new StringBuilder();
		var sw = new StringWriter(sb);
		using (var writer = new JsonTextWriter(sw)) {
			var serializer = JsonSerializer.Create(Json.JsonSettings);
			serializer.Serialize(writer, nearSide);
		}

		var ms2 = new MemoryStream(array);

		using (var reader = new BsonDataReader(ms2)) {
			var serializer = JsonSerializer.Create(Json.JsonSettings);
			farSide = serializer.Deserialize<TestCommands.FailedResponse>(reader);
		}

		Assert.NotNull(farSide);
		Assert.Equal(nearSide.MsgId, farSide.MsgId);
		Assert.Equal(nearSide.GetType(), farSide.GetType());
		Assert.Equal(nearSide.CorrelationId, farSide.CorrelationId);
		Assert.Equal(nearSide.CommandType, farSide.CommandType);
		Assert.Equal(nearSide.CommandId, farSide.CommandId);
		Assert.Equal(nearSide.CausationId, farSide.CausationId);
		Assert.Equal(nearSide.Exception?.Message, farSide.Exception?.Message);

		Assert.Equal(nearSide.Data, farSide.Data);
	}
	[Fact]
	public void can_serialize_json_fail_commandresponse() {
		var cmd = new TestCommands.TypedResponse(false);
		var nearSide = cmd.Fail(new CommandException("O_Ops", cmd), 15);
		TestCommands.FailedResponse? farSide;


		var sb = new StringBuilder();
		var sw = new StringWriter(sb);
		using (var writer = new JsonTextWriter(sw)) {
			var serializer = JsonSerializer.Create(Json.JsonSettings);
			serializer.Serialize(writer, nearSide);
		}

		using (var reader = new JsonTextReader(new StringReader(sb.ToString()))) {
			var serializer = JsonSerializer.Create(Json.JsonSettings);
			serializer.SerializationBinder = new DefaultSerializationBinder();
			serializer.ContractResolver = new TestContractResolver();
			farSide = serializer.Deserialize<TestCommands.FailedResponse>(reader);
		}

		Assert.NotNull(farSide);
		Assert.Equal(nearSide.MsgId, farSide.MsgId);
		Assert.Equal(nearSide.GetType(), farSide.GetType());
		Assert.Equal(nearSide.CorrelationId, farSide.CorrelationId);
		Assert.Equal(nearSide.CommandType, farSide.CommandType);
		Assert.Equal(nearSide.CommandId, farSide.CommandId);
		Assert.Equal(nearSide.CausationId, farSide.CausationId);
		Assert.Equal(nearSide.Exception?.Message, farSide.Exception?.Message);
		Assert.Equal(nearSide.Data, farSide.Data);
	}
}
public class TestContractResolver : DefaultContractResolver {
	protected override IList<JsonProperty> CreateConstructorParameters(ConstructorInfo constructor, JsonPropertyCollection memberProperties) {
		var result = base.CreateConstructorParameters(constructor, memberProperties);
		return result;
	}

	protected override JsonContract CreateContract(Type objectType) {
		var result = base.CreateContract(objectType);
		return result;
	}

	protected override JsonObjectContract CreateObjectContract(Type objectType) {
		var result = base.CreateObjectContract(objectType);
		return result;
	}
}
