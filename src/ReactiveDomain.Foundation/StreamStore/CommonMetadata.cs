using System;
using System.Collections.Generic;
using System.Text;

namespace ReactiveDomain.Foundation.StreamStore
{
    public class CommonMetadata
    {
        public int Version = 1; //legacy string dictionary metadata is implicitly version 0 
        public Guid CommitId { get; set; }
        public string AggregateName { get; set; }
        public string EventName { get; set; }
        public string EventAssembly { get; set; }
        public string EventFullyQualifiedName { get; set; }
    }
}
