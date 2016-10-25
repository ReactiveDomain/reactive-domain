using EventStore.ClientAPI;
using JustGiving.EventStore.Http.Client;
using JustGiving.EventStore.Http.SubscriberHost;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReactiveDomain.EventStore;
using ReactiveDomain.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections;

namespace ReactiveDomain.Bus
{
    public class EventStoreBusConnector :
        IHandle<Message>
    {
        //todo: handle rolling streams without dropping messages (likely at a higher level)
        private readonly BusAdapter _bus;
        private readonly IEventStoreConnection _es;
        private readonly IEventStoreHttpConnection _conn;
        private readonly IEventStreamSubscriber _subscriber;
        private readonly string _stream;
        private int ExpectedVersion = -2; //ExpectedVersion.Any
        private string _name;


        public EventStoreBusConnector(
                            IGeneralBus bus,
                            IEventStoreHttpConnection conn,
                            string stream,
                            string name)
        {
            _conn = conn;
            _subscriber = EventStreamSubscriber.Create(_conn, HandleJsonEvent, new MemoryBackedStreamPositionRepositoryForDebugging());
            _stream = stream;
            _name = name;
            _bus = new BusAdapter(bus);
            _bus.Subscribe(this);
            _subscriber.SubscribeTo(stream);
        }
        public EventStoreBusConnector(
                            IGeneralBus bus,
                            IEventStoreConnection es,
                            string stream,
                            string name)
        {
            _es = es;
            _stream = stream;
            _bus = new BusAdapter(bus);
            _name = name;
            _bus.Subscribe(this);
            _es.SubscribeToStreamAsync(stream, true, EventAppeared, SubscriptionDropped);
        }
        private void EventAppeared(EventStoreSubscription subscription, ResolvedEvent evt)
        {
            try
            {
                dynamic message = evt.DeserializeEvent();
                if (message is Message)
                    _bus.Handle(message);
            }
            catch
            {
                //ignore
            }
        }
        private void SubscriptionDropped(EventStoreSubscription arg1, SubscriptionDropReason arg2, Exception arg3)
        {
            _es.SubscribeToStreamAsync(_stream, true, EventAppeared, SubscriptionDropped);
        }


        public void Handle(Message message)
        {
            if (_es != null)
                PostToTcpClient(message);
            else
                PostToHttpClient(message);

        }

        private void PostToHttpClient(Message message)
        {
            try
            {

                var commitHeaders = new Dictionary<string, object>
            {
                {GetEventStoreRepository.CommitIdHeader, Guid.NewGuid()},
                {GetEventStoreRepository.EventClrTypeHeader, message.GetType().AssemblyQualifiedName},
                {"Source", _name}
            };
                // var evt = GetEventStoreRepository.ToEventData(message.MsgId, message, commitHeaders);
                var events = new[] { new NewEventData(message.MsgId, message.GetType().Name, message, commitHeaders) };
                _conn.AppendToStreamAsync(_stream, ExpectedVersion, events);
            }
            catch
            {
                //ignore
            }
        }

        private void PostToTcpClient(Message message)
        {
            try
            {
                var commitHeaders = new Dictionary<string, object>
            {
                {GetEventStoreRepository.CommitIdHeader, Guid.NewGuid()},
                {GetEventStoreRepository.AggregateClrTypeHeader, message.GetType().AssemblyQualifiedName},
                {"Source", _name}
            };
                var events = new[] { GetEventStoreRepository.ToEventData(message.MsgId, message, commitHeaders) };
                _es.AppendToStreamAsync(_stream, ExpectedVersion, events);
            }
            catch
            {
                //ignore
            }
        }

        public void HandleJsonEvent(EventInfo @event)
        {
            try
            {
                JToken eventClrTypeName = @event.Content.Metadata.SelectToken("EventClrTypeName");
                var typeName = (string)eventClrTypeName;
                //todo: remove this
                //hack conversion for namespace
                var type = Type.GetType(typeName);
                if (type == null)
                {
                    var fullname = typeName.Split(',').First();
                    var name = fullname.Split('.').Last();
                    typeName =
                        $"Greylock.StudyManagement.Messages.{name}, Greylock.StudyManagement, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null";
                    type = Type.GetType(typeName);
                }
                dynamic message = JsonConvert.DeserializeObject(@event.Content.Data.ToString(), type,
                    new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.Populate });
                if (message is Message)
                    _bus.Handle(message);
            }
            catch
            {
                //ignore
            }
        }
    }
}
