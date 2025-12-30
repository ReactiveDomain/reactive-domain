using ReactiveDomain.Messaging;

namespace SimpleMetadata;

public class Messages
{
    public record Greeting(string Text) : Message;

    public record Farewell(string Text) : Message;

    public class Sender
    {
        public string Name;
    }
}