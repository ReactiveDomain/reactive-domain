using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Transport.Serialization;
using Xunit;

namespace ReactiveDomain.Transport.Tests
{
    [Collection("TCP bus tests")]
    public class TcpBusClientSideTests
    {
        private readonly string _shortProp = "abc";
        // 16kb is large enough to cause the transport to split up the frame.
        // It would be better if we did the splitting manually so we were sure it really happened.
        // Would require mocking more things.
        private readonly string _longProp = string.Join("", Enumerable.Repeat("a", 16 * 1024));

        private readonly Dispatcher _localBus = new Dispatcher("local");

        [Fact]
        public void can_handle_split_frames()
        {
            var hostAddress = IPAddress.Loopback;
            var port = 10000;
            var tcs = new TaskCompletionSource<IMessage>();

            var handler = new WoftamCommandHandler(_longProp, true);
            var subscription = _localBus.Subscribe(handler);

            // server side
            var serverInbound = new QueuedHandler(
                new AdHocHandler<IMessage>(m => { if (m is Command cmd) _localBus.TrySend(cmd, out _); }),
                "InboundMessageServerHandler",
                true,
                TimeSpan.FromMilliseconds(1000));

            var tcpBusServerSide = new TcpBusServerSide(
                hostAddress,
                port,
                inboundNondiscardingMessageTypes: new[] { typeof(WoftamCommand) },
                inboundNondiscardingMessageQueuedHandler: serverInbound);

            _localBus.SubscribeToAll(tcpBusServerSide);

            serverInbound.Start();

            // client side
            var clientInbound = new QueuedHandler(
                new AdHocHandler<IMessage>(tcs.SetResult),
                "InboundMessageQueuedHandler",
                true,
                TimeSpan.FromMilliseconds(1000));

            var tcpBusClientSide = new TcpBusClientSide(
                hostAddress,
                port,
                inboundNondiscardingMessageTypes: new[] { typeof(CommandResponse) },
                inboundNondiscardingMessageQueuedHandler: clientInbound,
                messageSerializers: new Dictionary<Type, IMessageSerializer>
                    { { typeof(WoftamCommandResponse), new WoftamCommandResponse.Serializer() } });

            clientInbound.Start();

            // wait for tcp connection to be established (maybe an api to detect this would be nice)
            Thread.Sleep(TimeSpan.FromMilliseconds(200));

            // First send the command to server so it knows where to send the response.
            // We don't need either of these properties to be large since we're only testing
            // message splitting from server to client.
            tcpBusClientSide.Handle(MessageBuilder.New(() => new WoftamCommand(_shortProp)));

            // expect to receive it on the client side
            var gotMessage = tcs.Task.Wait(TimeSpan.FromMilliseconds(1000));
            Assert.True(gotMessage);
            var response = Assert.IsType<WoftamCommandResponse>(tcs.Task.Result);
            Assert.Equal(_longProp, response.PropertyA);

            subscription.Dispose();
            tcpBusClientSide.Dispose();
            tcpBusServerSide.Dispose();
        }
    }

    public class WoftamCommand : Command
    {
        public readonly string Property1;

        public WoftamCommand(string property1)
        {
            Property1 = property1;
        }
    }

    public class WoftamCommandResponse : Success
    {
        public readonly string PropertyA;

        public WoftamCommandResponse(WoftamCommand source, string propertyA)
            : base(source)
        {
            PropertyA = propertyA;
        }

        public class Serializer : IMessageSerializer
        {
            public IMessage DeserializeMessage(string json, Type messageType)
            {
                var reader = new JsonTextReader(new StringReader(json));
                var propA = "";
                var correlationId = Guid.Empty;
                var causationId = Guid.Empty;
                WoftamCommand sourceCommand = null;
                while (reader.Read())
                {
                    if (reader.TokenType == JsonToken.PropertyName)
                    {
                        if (reader.Value.ToString() == nameof(PropertyA))
                        {
                            reader.Read();
                            propA = reader.Value.ToString();
                        }
                        else if (reader.Value.ToString() == "CorrelationId")
                        {
                            reader.Read();
                            correlationId = Guid.Parse(reader.Value.ToString());
                        }
                        else if (reader.Value.ToString() == "CausationId")
                        {
                            reader.Read();
                            causationId = Guid.Parse(reader.Value.ToString());
                        }
                        else if (reader.Value.ToString() == "SourceCommand")
                        {
                            reader.Read();
                            var serializer = new JsonSerializer();
                            sourceCommand = serializer.Deserialize<WoftamCommand>(reader);
                            break;
                        }
                    }
                }
                if (sourceCommand is null)
                    throw new JsonSerializationException("Could not deserialize WoftamCommandResponse.");
                var response = new WoftamCommandResponse(sourceCommand, propA);
                if (correlationId != Guid.Empty)
                    response.CorrelationId = correlationId;
                if (causationId != Guid.Empty)
                    response.CausationId = causationId;
                return response;
            }

            public string SerializeMessage(IMessage message)
            {
                return JsonConvert.SerializeObject(message, Json.JsonSettings);
            }
        }
    }

    public class WoftamCommandHandler : IHandleCommand<WoftamCommand>
    {
        private readonly string _prop;
        private readonly bool _returnCustomResponse;

        public WoftamCommandHandler(string prop, bool returnCustomResponse)
        {
            _prop = prop;
            _returnCustomResponse = returnCustomResponse;
        }
        public CommandResponse Handle(WoftamCommand command)
        {
            return _returnCustomResponse ? new WoftamCommandResponse(command, _prop) : command.Succeed();
        }
    }
}
