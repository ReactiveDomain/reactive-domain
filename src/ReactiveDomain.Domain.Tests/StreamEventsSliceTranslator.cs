using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace ReactiveDomain.Domain.Tests
{
    public class StreamEventsSliceTranslator : IStreamEventsSliceTranslator
    {
        public JsonSerializerSettings Settings { get; }
        public MessageTypeResolver Resolver { get; }

        public StreamEventsSliceTranslator(MessageTypeResolver resolver, JsonSerializerSettings settings)
        {
            Resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public IEnumerable<object> Translate(StreamEventsSlice slice)
        {
            if (slice == null)
                throw new ArgumentNullException(nameof(slice));

            foreach (var @event in slice.Events)
            {
                var originalEvent = @event.OriginalEvent;
                var typeOfEvent = Resolver(originalEvent.EventType);
                var translated = JsonConvert.DeserializeObject(
                    Encoding.UTF8.GetString(originalEvent.Data), 
                    typeOfEvent,
                    Settings);
                yield return translated;
            }
        }
    }
}