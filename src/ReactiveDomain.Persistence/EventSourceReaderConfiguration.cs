using System;

namespace ReactiveDomain
{
    public class EventSourceReaderConfiguration
    {
        public EventSourceReaderConfiguration(
            StreamNameConverter converter, 
            Func<IStreamEventsSliceTranslator> translatorFactory, 
            SliceSize sliceSize)
        {
            Converter = converter ?? throw new ArgumentNullException(nameof(converter));
            TranslatorFactory = translatorFactory ?? throw new ArgumentNullException(nameof(translatorFactory));
            SliceSize = sliceSize;
        }

        public StreamNameConverter Converter { get; }
        public Func<IStreamEventsSliceTranslator> TranslatorFactory { get; }
        // Balance between Network, Memory, Roundtrips and stream length
        public SliceSize SliceSize { get; }
    }
}