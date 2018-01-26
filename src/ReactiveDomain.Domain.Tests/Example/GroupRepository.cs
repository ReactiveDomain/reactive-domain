using System;
using System.Threading;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using ReactiveDomain;

namespace ReactiveDomain.Example
{
    public class GroupRepository : Repository<Group>
    {
        public GroupRepository(IEventStoreConnection connection, EventSourceReaderConfiguration readerConfiguration, EventSourceWriterConfiguration writerConfiguration) 
            : base(Group.Factory, connection, readerConfiguration, writerConfiguration)
        {
        }
    }
}