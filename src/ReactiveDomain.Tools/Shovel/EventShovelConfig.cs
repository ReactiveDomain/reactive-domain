using EventStore.ClientAPI;
using EventStore.ClientAPI.SystemData;
using System.Collections.Generic;

namespace Shovel
{
    public class EventShovelConfig
    {
        public IEventStoreConnection SourceConnection { get; set; }

        public IEventStoreConnection TargetConnection { get; set; }

        public UserCredentials SourceCredentials { get; set; }

        public UserCredentials TargetCredentials { get; set; }

        public IEventTransformer EventTransformer { get; set; }

        public IList<string> StreamFilter { get; }

        public IList<string> StreamWildcardFilter { get; }

        public IList<string> EventTypeFilter { get; }

        public IList<string> EventTypeWildcardFilter { get; }

        public EventShovelConfig()
        {
            StreamFilter = new List<string>();
            StreamWildcardFilter = new List<string>();
            EventTypeFilter = new List<string>();
            EventTypeWildcardFilter = new List<string>();
        }
    }
}
