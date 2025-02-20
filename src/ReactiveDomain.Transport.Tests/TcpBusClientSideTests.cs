using Newtonsoft.Json;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Xunit;

namespace ReactiveDomain.Transport.Tests
{
    [Collection("TCP bus tests")]
    public class TcpBusClientSideTests : IDisposable
    {
        private const string ShortProp = "abc";
        // 16kb is large enough to cause the transport to split up the frame.
        // It would be better if we did the splitting manually so we were sure it really happened.
        // Would require mocking more things.
        private readonly string _longProp = string.Join("", Enumerable.Repeat("a", 16 * 1024));

        private readonly Dispatcher _localBus = new Dispatcher("local");
        private readonly IList<IDisposable> _subscriptions = new List<IDisposable>();
        private readonly TcpBusServerSide _tcpBusServerSide;
        private readonly TcpBusClientSide _tcpBusClientSide;
        private readonly TaskCompletionSource<IMessage> _tcs;

        public TcpBusClientSideTests()
        {
            var hostAddress = IPAddress.Loopback;
            var port = 10008;
#if NET6_0
            port = 10006; //net 6 and 8 will fight over the port in tests
#endif
            _tcs = new TaskCompletionSource<IMessage>();

            // server side
            var serverInbound = new QueuedHandler(
                new AdHocHandler<IMessage>(m => { if (m is Command cmd) _localBus.TrySend(cmd, out _); }),
                "InboundMessageServerHandler",
                true,
                TimeSpan.FromMilliseconds(1000));

            _tcpBusServerSide = new TcpBusServerSide(
                hostAddress,
                port,
                inboundNondiscardingMessageTypes: new[] { typeof(WoftamCommand) },
                inboundNondiscardingMessageQueuedHandler: serverInbound);

            _localBus.SubscribeToAll(_tcpBusServerSide);

            serverInbound.Start();

            // client side
            var clientInbound = new QueuedHandler(
                new AdHocHandler<IMessage>(_tcs.SetResult),
                "InboundMessageQueuedHandler",
                true,
                TimeSpan.FromMilliseconds(1000));

            _tcpBusClientSide = new TcpBusClientSide(
                hostAddress,
                port,
                inboundNondiscardingMessageTypes: new[] { typeof(CommandResponse) },
                inboundNondiscardingMessageQueuedHandler: clientInbound,
                messageSerializers: new Dictionary<Type, Serialization.IMessageSerializer>
                    { { typeof(WoftamCommandResponse), new WoftamCommandResponse.Serializer() } });

            clientInbound.Start();

            // wait for tcp connection to be established
            AssertEx.IsOrBecomesTrue(() => _tcpBusClientSide.IsConnected, 200);
        }

        [Fact]
        public async Task can_send_command()
        {
            var handler = new WoftamCommandHandler(_longProp);
            _subscriptions.Add(_localBus.Subscribe(handler));

            // First send the command to server so it knows where to send the response.
            _tcpBusClientSide.Handle(MessageBuilder.New(() => new WoftamCommand(ShortProp)));

            // expect to receive it on the client side
            var result = await _tcs.Task.WaitAsync(TimeSpan.FromMilliseconds(1000), TestContext.Current.CancellationToken);
            Assert.IsType<Success>(result);
        }

        [Fact]
        public async Task can_handle_split_frames() // Also tests custom deserializer
        {
            var handler = new WoftamCommandHandler(_longProp) { ReturnCustomResponse = true };
            _subscriptions.Add(_localBus.Subscribe(handler));

            // First send the command to server so it knows where to send the response.
            // We don't need this properties to be large since we're only testing message
            // splitting from server to client.
            _tcpBusClientSide.Handle(MessageBuilder.New(() => new WoftamCommand(ShortProp)));

            // expect to receive it on the client side
            var result = await _tcs.Task.WaitAsync(TimeSpan.FromMilliseconds(1000), TestContext.Current.CancellationToken);
            var response = Assert.IsType<WoftamCommandResponse>(result);
            Assert.Equal(_longProp, response.PropertyA);
        }

        public void Dispose()
        {
            foreach (var subscription in _subscriptions)
            {
                subscription.Dispose();
            }
            _localBus?.Dispose();
            _tcpBusClientSide.Dispose();
            _tcpBusServerSide.Dispose();
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

        public class Serializer : Serialization.IMessageSerializer
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
        public bool ReturnCustomResponse { get; set; }

        public WoftamCommandHandler(string prop)
        {
            _prop = prop;
        }
        public CommandResponse Handle(WoftamCommand command)
        {
            return ReturnCustomResponse ? new WoftamCommandResponse(command, _prop) : command.Succeed();
        }
    }
}
