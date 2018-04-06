using System.Collections.Generic;

namespace ReactiveDomain.Foundation.EventStore
{
    public interface IEventSerializer
    {
        byte[] Serialize(object data);
        byte[] Serialize(IDictionary<string, object> data);
        object Deserialize(byte[] metadata, byte[] data, string clrQualifiedTypeHeader);
    }
}
