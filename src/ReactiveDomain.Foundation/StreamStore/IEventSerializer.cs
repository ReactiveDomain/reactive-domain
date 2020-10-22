using System;
using System.Collections.Generic;

// ReSharper disable once CheckNamespace
namespace ReactiveDomain.Foundation {
    public interface IEventSerializer {
        EventData Serialize(object @event, IDictionary<string, object> headers = null);
        object Deserialize(IEventData @event);
        Type FindType(string typeName);
    }
}
