using System;
using System.Collections.Generic;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;

namespace ReactiveDomain.Foundation.EventStore
{
    public class EventStoreMessageLogger :
        QueuedSubscriber,
        IHandle<Message>
    {
        private readonly IStreamStoreConnection _eventStore;
        private readonly string _streamPrefix;
        private readonly IGeneralBus _bus;

        public EventStoreMessageLogger(
            IGeneralBus bus,
            IStreamStoreConnection es,
            string logStreamPrefix = "",
            bool enableLogging = false,
            bool idempotent = true) : base(bus, idempotent)
        {
            _bus = bus;
            _eventStore = es;
            Enabled = enableLogging;
            _streamPrefix = logStreamPrefix;
            if (Enabled)
                _eventStore?.ConnectAsync();

            // ReSharper disable once RedundantTypeArgumentsOfMethod
            Subscribe<Message>(this);
        }

        public bool Enabled { get; set; }
        //TODO:  eventually, connect on enable, disconnect on disable?


        public string FullStreamName
        {
            get
            {
                int amPm = (DateTime.UtcNow.Hour > 12) ? 2 : 1;                
                return $"{_streamPrefix}-Log-{DateTime.UtcNow.Year}-{DateTime.UtcNow.Month}-{DateTime.UtcNow.Day}.{amPm}";
            }

        }


        public void Handle(Message message)
        {
            if (!Enabled) return;
            if (_eventStore == null) return;

            var metadata = new Dictionary<string, object>
            {
                {"Bus", _bus.Name},
                {"TimeStamp", DateTime.UtcNow.ToString("yyyy.MM.dd.HH:mm:ss:ffffff")}
            };


            var ed = EventStoreRepository.ToEventData(message.MsgId, message, metadata);
            var data = new List<EventData> {ed};

           // int amPm = (DateTime.UtcNow.Hour > 12) ? 2 : 1;
            _eventStore.AppendToStreamAsync(
                FullStreamName,
                ExpectedVersion.Any,
                data.ToArray());
        }

    }
}
