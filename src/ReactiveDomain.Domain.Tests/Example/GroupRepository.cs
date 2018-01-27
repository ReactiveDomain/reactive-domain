using System;
using System.Threading;
using System.Threading.Tasks;
using EventStore.ClientAPI;

namespace ReactiveDomain.Example
{
    public class GroupRepository : IGroupRepository
    {
        private readonly EventSourceReader _reader;
        private readonly EventSourceWriter _writer;

        public GroupRepository(IEventStoreConnection connection, EventSourceReaderConfiguration readerConfiguration, EventSourceWriterConfiguration writerConfiguration)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));
            if (readerConfiguration == null)
                throw new ArgumentNullException(nameof(readerConfiguration));
            if (writerConfiguration == null)
                throw new ArgumentNullException(nameof(writerConfiguration));

            _reader = new EventSourceReader(
                Group.Factory,
                connection,
                readerConfiguration);
            _writer = new EventSourceWriter(
                connection,
                writerConfiguration);
        }

        public async Task<Group> LoadById(GroupIdentifier identifier, CancellationToken ct = default(CancellationToken))
        {
            var stream = new StreamName($"groups-{identifier.ToGuid():N}");
            var result = await _reader.ReadStreamAsync(stream, ct);
            switch (result.State)
            {
                case ReadResultState.NotFound:
                    throw new GroupNotFoundException(identifier);
                case ReadResultState.Deleted:
                    throw new GroupDeletedException(identifier);
            }
            return (Group)result.Value;
        }

        public async Task<Group> TryLoadById(GroupIdentifier identifier, CancellationToken ct = default(CancellationToken))
        {
            var stream = new StreamName($"groups-{identifier.ToGuid():N}");
            var result = await _reader.ReadStreamAsync(stream, ct);
            if (result.State == ReadResultState.Deleted)
                throw new GroupDeletedException(identifier);
            return result.State == ReadResultState.Found ? (Group)result.Value : null;
        }

        public async Task Save(Group @group, Guid causation, Guid correlation, Metadata metadata, CancellationToken ct = default(CancellationToken))
        {
            var stream = new StreamName($"groups-{group.Id.ToGuid():N}");
            var result = await _writer.WriteStreamAsync(stream, group, causation, correlation, metadata, ct);
            //CQS violation - only required if an event source instance is to handle multiple commands or is kept in memory.
            ((IEventSource) group).ExpectedVersion = result.NextExpectedVersion;
        }
    }
}