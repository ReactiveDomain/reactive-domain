using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using Newtonsoft.Json.Serialization;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;
using Xunit;

namespace ReactiveDomain.Messaging.Tests;

public class when_serializing_commands {
    [Fact]
    public void can_serialize_bson_success_commandresponse() {
        var cmd = new TestCommands.TypedResponse(false);
        var nearSide = cmd.Succeed(15);
        TestCommands.TestResponse farSide;
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
        TestCommands.TestResponse farSide;


        var sb = new StringBuilder();
        var sw = new StringWriter(sb);
        using (var writer = new JsonTextWriter(sw)) {
            var serializer = JsonSerializer.Create(Json.JsonSettings);
            serializer.Serialize(writer, nearSide);
        }

        using (var reader = new JsonTextReader(new StringReader(sb.ToString()))) {
            var serializer = JsonSerializer.Create(Json.JsonSettings);
            serializer.SerializationBinder = new TestDeserializer();
            serializer.ContractResolver = new TestContractResolver();
            farSide = serializer.Deserialize<TestCommands.TestResponse>(reader);
        }

        Assert.Equal(nearSide.MsgId, farSide.MsgId);
        Assert.Equal(nearSide.GetType(), farSide.GetType());
        Assert.Equal(nearSide.CorrelationId, farSide.CorrelationId);
        Assert.Equal(nearSide.CommandType, farSide.CommandType);
        Assert.Equal(nearSide.CommandId, farSide.CommandId);
        Assert.Equal(nearSide.CausationId, farSide.CausationId);

        Assert.Equal(nearSide.Data, farSide.Data);
    }

    [Fact]
    public void can_serialize_bson_fail_commandresponse() {
        var cmd = new TestCommands.TypedResponse(false);
        var nearSide = cmd.Fail(new CommandException("O_Ops", cmd), 15);
        TestCommands.FailedResponse farSide;
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

        Assert.Equal(nearSide.MsgId, farSide.MsgId);
        Assert.Equal(nearSide.GetType(), farSide.GetType());
        Assert.Equal(nearSide.CorrelationId, farSide.CorrelationId);
        Assert.Equal(nearSide.CommandType, farSide.CommandType);
        Assert.Equal(nearSide.CommandId, farSide.CommandId);
        Assert.Equal(nearSide.CausationId, farSide.CausationId);
        Assert.Equal(nearSide.Exception.Message, farSide.Exception.Message);

        Assert.Equal(nearSide.Data, farSide.Data);
    }
    [Fact]
    public void can_serialize_json_fail_commandresponse() {
        var cmd = new TestCommands.TypedResponse(false);
        var nearSide = cmd.Fail(new CommandException("O_Ops", cmd), 15);
        TestCommands.FailedResponse farSide;


        var sb = new StringBuilder();
        var sw = new StringWriter(sb);
        using (var writer = new JsonTextWriter(sw)) {
            var serializer = JsonSerializer.Create(Json.JsonSettings);
            serializer.Serialize(writer, nearSide);
        }

        using (var reader = new JsonTextReader(new StringReader(sb.ToString()))) {
            var serializer = JsonSerializer.Create(Json.JsonSettings);
            serializer.SerializationBinder = new TestDeserializer();
            serializer.ContractResolver = new TestContractResolver();
            farSide = serializer.Deserialize<TestCommands.FailedResponse>(reader);
        }

        Assert.Equal(nearSide.MsgId, farSide.MsgId);
        Assert.Equal(nearSide.GetType(), farSide.GetType());
        Assert.Equal(nearSide.CorrelationId, farSide.CorrelationId);
        Assert.Equal(nearSide.CommandType, farSide.CommandType);
        Assert.Equal(nearSide.CommandId, farSide.CommandId);
        Assert.Equal(nearSide.CausationId, farSide.CausationId);
        Assert.Equal(nearSide.Exception.Message, farSide.Exception.Message);
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

public class TestDeserializer : DefaultSerializationBinder {
    public override Type BindToType(string assemblyName, string typeName) {
        return base.BindToType(assemblyName, typeName);
    }

    public override void BindToName(Type serializedType, out string assemblyName, out string typeName) {
        base.BindToName(serializedType, out assemblyName, out typeName);
    }
}