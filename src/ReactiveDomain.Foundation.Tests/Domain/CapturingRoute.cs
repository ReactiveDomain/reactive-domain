namespace ReactiveDomain.Foundation.Tests.Domain
{
    public class CapturingRoute
    {
        public CapturingRoute()
        {
            Captured = null;
        }

        public void Capture<TEvent>(TEvent result)
        {
            Captured = result;
        }

        public object Captured { get; private set; }
    }
}