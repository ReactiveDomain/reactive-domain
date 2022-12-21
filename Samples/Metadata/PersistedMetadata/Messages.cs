using ReactiveDomain.Messaging;

namespace Metadata_Sample_App
{
    public class Messages
    {
        public class Greeting : Message
        {
            public readonly string Text;
            public Greeting(string text)
            {
                Text = text;
            }
        }
      public class Farewell : Message
        {
            public readonly string Text;
            public Farewell(string text)
            {
                Text = text;
            }
        }
        public class Sender
        {
            public string Name;
        }
    }
}
