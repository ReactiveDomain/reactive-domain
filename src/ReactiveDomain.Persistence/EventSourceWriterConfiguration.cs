using System;

namespace ReactiveDomain
{
    public class EventSourceWriterConfiguration
    {
        public StreamNameConverter Converter { get; }
        public IEventSourceChangesetTranslator Translator { get; }

        public EventSourceWriterConfiguration(
            StreamNameConverter converter, 
            IEventSourceChangesetTranslator translator)
        {
            Converter = converter ?? throw new ArgumentNullException(nameof(converter));
            Translator = translator ?? throw new ArgumentNullException(nameof(translator));
        }
    }
}