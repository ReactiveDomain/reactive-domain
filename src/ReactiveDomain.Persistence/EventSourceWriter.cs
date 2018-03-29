using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ReactiveDomain
{
    public class EventSourceWriter
    {
        public IStreamStoreConnection Connection { get; }
        public EventSourceWriterConfiguration Configuration { get; }

        public EventSourceWriter(IStreamStoreConnection connection, EventSourceWriterConfiguration configuration)
        {
            Connection = connection ?? throw new ArgumentNullException(nameof(connection));
            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public async Task<WriteResult> WriteStreamAsync(
            StreamName stream, 
            IEventSource source, 
            Guid causation,
            Guid correlation,
            Metadata metadata = null, 
            CancellationToken ct = default(CancellationToken))
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var resolvedStream = Configuration.Converter(stream);
            ct.ThrowIfCancellationRequested();
            var result = await Connection.AppendToStreamAsync(
                resolvedStream, 
                source.ExpectedVersion, 
                Configuration.Translator.Translate(
                    new EventSourceChangeset(
                        resolvedStream,
                        source.ExpectedVersion,
                        causation,
                        correlation,
                        metadata ?? Metadata.None,
                        source.TakeEvents()
                    )).ToArray()
                    )
                .ConfigureAwait(false);
            source.ExpectedVersion = result.NextExpectedVersion;
            return result;
        }
    }
}