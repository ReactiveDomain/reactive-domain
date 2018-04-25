using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace ReactiveDomain.Domain.Tests
{
    public class EventSourceChangesetTranslator : IEventSourceChangesetTranslator
    {
        private static readonly IdempotentEventIdGenerator Generator = new IdempotentEventIdGenerator();

        public EventSourceChangesetTranslator(MessageNameResolver resolver, JsonSerializerSettings settings)
        {
            Resolver = resolver ?? throw new System.ArgumentNullException(nameof(resolver));
            Settings = settings ?? throw new System.ArgumentNullException(nameof(settings));
        }

        public MessageNameResolver Resolver { get; }
        public JsonSerializerSettings Settings { get; }
        
        public IEnumerable<EventData> Translate(EventSourceChangeset changeset)
        {
            if (changeset == null)
            {
                throw new System.ArgumentNullException(nameof(changeset));
            }

            return changeset.Events.Select((@event, index) =>
            {
                var name = Resolver(@event.GetType());
                return new EventData(
                    Generator.Generate(changeset.Causation, changeset.ExpectedVersion, name, index),
                    name,
                    true,
                    Encoding.UTF8.GetBytes(
                        JsonConvert.SerializeObject(
                            @event,
                            Settings)),
                    Encoding.UTF8.GetBytes(
                        JsonConvert.SerializeObject(
                            changeset.Metadata.ToKeyValuePairs(),
                            Settings)));
            });
        }
    }
}