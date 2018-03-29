using System;
using System.Threading;
using System.Threading.Tasks;


namespace ReactiveDomain
{
    public class EventSourceReader
    {
        public Func<IEventSource> Factory { get; }
        public IStreamStoreConnection Connection { get; }
        public EventSourceReaderConfiguration Configuration { get; }

        public EventSourceReader(
            Func<IEventSource> factory,
            IStreamStoreConnection connection,
            EventSourceReaderConfiguration configuration)
        {
            Factory = factory ?? throw new ArgumentNullException(nameof(factory));
            Connection = connection ?? throw new ArgumentNullException(nameof(connection));
            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public async Task<ReadResult> ReadStreamAsync(StreamName stream, CancellationToken ct = default(CancellationToken))
        {
            ct.ThrowIfCancellationRequested();
            var resolvedStream = Configuration.Converter(stream);
            var slice = await Connection.ReadStreamEventsForwardAsync(
                resolvedStream, 
                StreamPosition.Start, 
                Configuration.SliceSize, 
                false)
                .ConfigureAwait(false);
            switch (slice.Status)
            {
                case SliceReadStatus.StreamNotFound:
                    return ReadResult.NotFound;
                case SliceReadStatus.StreamDeleted:
                    return ReadResult.Deleted;
            }
            var eventSource = Factory();
            var translator = Configuration.TranslatorFactory();
            eventSource.RestoreFromEvents(translator.Translate(slice));
            while (!slice.IsEndOfStream)
            {
                ct.ThrowIfCancellationRequested();
                slice = await Connection.ReadStreamEventsForwardAsync(
                    resolvedStream, 
                    slice.NextEventNumber, 
                    Configuration.SliceSize, 
                    false)
                    .ConfigureAwait(false);
                switch (slice.Status)
                {
                    case SliceReadStatus.StreamNotFound:
                        return ReadResult.NotFound;
                    case SliceReadStatus.StreamDeleted:
                        return ReadResult.Deleted;
                }
                eventSource.RestoreFromEvents(translator.Translate(slice));
            }
            eventSource.ExpectedVersion = slice.LastEventNumber;
            return ReadResult.Found(eventSource);
        }
    }
}
