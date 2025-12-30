using ReactiveDomain.Messaging.Bus;
using Terminal.Gui;

namespace SimpleMetadata;

internal class Program
{
    static void Main(string[] args)
    {
        Application.Init();
        Application.Top.Add(new MessageWindow(new InMemoryBus("message bus")));
        Application.Run();

        Application.Shutdown();
    }
}