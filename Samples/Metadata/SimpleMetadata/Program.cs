using ReactiveDomain.Messaging.Bus;
using Terminal.Gui;

namespace Metadata_Sample_App
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Application.Init();
            Application.Top.Add(new Metadata_Sample_App.MessageWindow(new InMemoryBus("message bus")));
            Application.Run();

            Application.Shutdown();
        }
    }
}