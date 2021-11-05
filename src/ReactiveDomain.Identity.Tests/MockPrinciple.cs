using ReactiveDomain.Identity.ReadModels;


namespace ReactiveDomain.Identity.Tests
{
    internal class MockPrinciple : IPrinciple
    {
        public string Provider { get; set; }

        public string Domain { get; set; }

        public string SId { get; set; }
    }
}
