using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using Newtonsoft.Json.Serialization;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Messaging.Testing;
using Xunit;

namespace ReactiveDomain.Messaging.Tests
{
    public class when_serializing_commands
    {
        [Fact]
        public void can_serialize_bson_success_commandresponse()
        {
            var cmd = new TestCommands.TypedTestCommand(Guid.NewGuid(), null);
            var nearSide = cmd.Succeed(15);
            TestCommands.TestCommandResponse farSide;
            var ms = new MemoryStream();
            using (var writer = new BsonWriter(ms))
            {
                var serializer = new JsonSerializer();
                serializer.Serialize(writer, nearSide);
            }
            var array = ms.ToArray();

            StringBuilder sb = new StringBuilder();
            StringWriter sw = new StringWriter(sb);
            using (var writer = new JsonTextWriter(sw))
            {
                var serializer = new JsonSerializer();
                serializer.Serialize(writer, nearSide);
            }
            var foo = sb.ToString();

            var ms2 = new MemoryStream(array);

            using (var reader = new BsonReader(ms2))
            {
                var serializer = new JsonSerializer();
                farSide = serializer.Deserialize<TestCommands.TestCommandResponse>(reader);
            }

            Assert.Equal(nearSide.MsgId, farSide.MsgId);
            Assert.Equal(nearSide.MsgTypeId, farSide.MsgTypeId);
            Assert.Equal(nearSide.CorrelationId, farSide.CorrelationId);
            Assert.Equal(nearSide.CommandType, farSide.CommandType);
            Assert.Equal(nearSide.CommandId, farSide.CommandId);
            Assert.Equal(nearSide.SourceId, farSide.SourceId);

            Assert.Equal(nearSide.Data, farSide.Data);
        }
        [Fact]
        public void can_serialize_json_success_commandresponse()
        {
            var cmd = new TestCommands.TypedTestCommand(Guid.NewGuid(), null);
            var nearSide = cmd.Succeed(15);
            TestCommands.TestCommandResponse farSide;


            StringBuilder sb = new StringBuilder();
            StringWriter sw = new StringWriter(sb);
            using (var writer = new JsonTextWriter(sw))
            {
                var serializer = new JsonSerializer();
                serializer.Serialize(writer, nearSide);
            }
            var foo = sb.ToString();



            using (var reader = new JsonTextReader(new StringReader(foo)))
            {
                var serializer = new JsonSerializer();
                serializer.Binder = new TestDeserializer();
                serializer.ContractResolver = new TestContractResolver();
                farSide = serializer.Deserialize<TestCommands.TestCommandResponse>(reader);
            }

            Assert.Equal(nearSide.MsgId, farSide.MsgId);
            Assert.Equal(nearSide.MsgTypeId, farSide.MsgTypeId);
            Assert.Equal(nearSide.CorrelationId, farSide.CorrelationId);
            Assert.Equal(nearSide.CommandType, farSide.CommandType);
            Assert.Equal(nearSide.CommandId, farSide.CommandId);
            Assert.Equal(nearSide.SourceId, farSide.SourceId);

            Assert.Equal(nearSide.Data, farSide.Data);
        }

        [Fact]
        public void can_serialize_bson_fail_commandresponse()
        {
            var cmd = new TestCommands.TypedTestCommand(Guid.NewGuid(), null);
            var nearSide = cmd.Fail(new CommandException("O_Ops", cmd), 15);
            TestCommands.TestFailedCommandResponse farSide;
            var ms = new MemoryStream();
            using (var writer = new BsonWriter(ms))
            {
                var serializer = new JsonSerializer();
                serializer.Serialize(writer, nearSide);
            }
            var array = ms.ToArray();

            StringBuilder sb = new StringBuilder();
            StringWriter sw = new StringWriter(sb);
            using (var writer = new JsonTextWriter(sw))
            {
                var serializer = new JsonSerializer();
                serializer.Serialize(writer, nearSide);
            }
            var foo = sb.ToString();

            var ms2 = new MemoryStream(array);

            using (var reader = new BsonReader(ms2))
            {
                var serializer = new JsonSerializer();
                farSide = serializer.Deserialize<TestCommands.TestFailedCommandResponse>(reader);
            }

            Assert.Equal(nearSide.MsgId, farSide.MsgId);
            Assert.Equal(nearSide.MsgTypeId, farSide.MsgTypeId);
            Assert.Equal(nearSide.CorrelationId, farSide.CorrelationId);
            Assert.Equal(nearSide.CommandType, farSide.CommandType);
            Assert.Equal(nearSide.CommandId, farSide.CommandId);
            Assert.Equal(nearSide.SourceId, farSide.SourceId);
            Assert.Equal(nearSide.Exception.Message, farSide.Exception.Message);

            Assert.Equal(nearSide.Data, farSide.Data);
        }
        [Fact]
        public void can_serialize_json_fail_commandresponse()
        {
            var cmd = new TestCommands.TypedTestCommand(Guid.NewGuid(), null);
            var nearSide = cmd.Fail(new CommandException("O_Ops",cmd), 15);
            TestCommands.TestFailedCommandResponse farSide;


            StringBuilder sb = new StringBuilder();
            StringWriter sw = new StringWriter(sb);
            using (var writer = new JsonTextWriter(sw))
            {
                var serializer = new JsonSerializer();
                serializer.Serialize(writer, nearSide);
            }
            var foo = sb.ToString();



            using (var reader = new JsonTextReader(new StringReader(foo)))
            {
                var serializer = new JsonSerializer();
                serializer.Binder = new TestDeserializer();
                serializer.ContractResolver = new TestContractResolver();
                farSide = serializer.Deserialize<TestCommands.TestFailedCommandResponse>(reader);
            }

            Assert.Equal(nearSide.MsgId, farSide.MsgId);
            Assert.Equal(nearSide.MsgTypeId, farSide.MsgTypeId);
            Assert.Equal(nearSide.CorrelationId, farSide.CorrelationId);
            Assert.Equal(nearSide.CommandType, farSide.CommandType);
            Assert.Equal(nearSide.CommandId, farSide.CommandId);
            Assert.Equal(nearSide.SourceId, farSide.SourceId);
            Assert.Equal(nearSide.Exception.Message, farSide.Exception.Message);
            Assert.Equal(nearSide.Data, farSide.Data);
        }
    }
    public class TestContractResolver : DefaultContractResolver
    {


        protected override IList<JsonProperty> CreateConstructorParameters(ConstructorInfo constructor, JsonPropertyCollection memberProperties)
        {
            var rslt = base.CreateConstructorParameters(constructor, memberProperties);
            return rslt;
        }

        protected override JsonContract CreateContract(Type objectType)
        {
            var rslt = base.CreateContract(objectType);
            return rslt;
        }



        protected override JsonObjectContract CreateObjectContract(Type objectType)
        {
            var rslt = base.CreateObjectContract(objectType);
            return rslt;

        }
    }

    public class TestDeserializer : DefaultSerializationBinder
    {
        public override Type BindToType(string assemblyName, string typeName)
        {
            return base.BindToType(assemblyName, typeName);
        }

        public override void BindToName(Type serializedType, out string assemblyName, out string typeName)
        {
            base.BindToName(serializedType, out assemblyName, out typeName);
        }
    }
}
