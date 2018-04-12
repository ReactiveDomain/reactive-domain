using System;
using System.Collections.Generic;

namespace ReactiveDomain.Foundation.EventStore {
    public interface IEventSerializer {
        EventData Serialize(object @event, IDictionary<string, object> headers = null);
        object Deserialize(IEventData @event);
    }
}
