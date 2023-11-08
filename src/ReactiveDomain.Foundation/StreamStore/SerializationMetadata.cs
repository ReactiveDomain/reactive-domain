using System;
using System.Collections.Generic;
using System.Text;

namespace ReactiveDomain.Foundation.StreamStore
{
    public class SerializationMetadata
    {
        public string Version = "1"; //legacy string dictionary metadata is implicitly version 0 
        public Guid CommitId { get; set; }
        public string AggregateClrTypeNameHeader { get; set; }
        public string AggregateClrTypeName { get; set; }
        public string EventClrTypeName { get; set; }
        public string EventClrQualifiedTypeName { get; set; }
    }
}
