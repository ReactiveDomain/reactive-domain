using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ReactiveDomain.Messaging;

namespace ReactiveDomain.Bus
{
    public interface IFilteredSubscriber:ISubscriber
    {
        void Subscribe<T>(IHandle<Message> handler) where T : Message;
        void Unsubscribe<T>(IHandle<Message> handler) where T : Message;
    }
}
