﻿using ReactiveDomain.IdentityStorage.ReadModels;

namespace ReactiveDomain.IdentityStorage.Tests
{
    internal class MockPrinciple : IPrinciple
    {
        public string Provider { get; set; }

        public string Domain { get; set; }

        public string SId { get; set; }
    }
}
