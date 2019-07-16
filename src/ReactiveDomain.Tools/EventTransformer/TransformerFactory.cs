using Shovel;

namespace EventTransformer
{
    public class TransformerFactory
    {
        public static IEventTransformer GetEventTransformer()
        {
            return new DummyEventTransformer();
        }
    }
}
