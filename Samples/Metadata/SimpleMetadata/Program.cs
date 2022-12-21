using ReactiveDomain.Messaging.Bus;
using Terminal.Gui;

namespace Sample1
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Application.Init();
            Application.Top.Add(new Sample1.MessageWindow(new InMemoryBus("message bus")));
            Application.Run();

            Application.Shutdown();
        }
    }
}