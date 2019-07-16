using System;

namespace ReactiveDomain.Foundation.Tests.SynchronizedStreamListenerTests.Common
{
    /// <summary>
    /// Generate stream names for testing. 
    /// </summary>
    /// <remarks>
    /// todo:
    /// The use of the extra Guid doesn't match the generation by the <see cref="ReactiveDomain.Foundation.IStreamNameBuilder"/>
    /// and the checks for existing category and event streams fail. <see cref="PrefixedCamelCaseStreamNameBuilderTests"/>
    /// tests the stream name generation. So switching to the PrefixedCamelCaseStreamNameBuilder. Leaving these here so Chris can
    /// agree and remove or correct me.
    /// end todo
    /// </remarks>
    internal class TestStreamNameBuilder : IStreamNameBuilder
    {
        private readonly Guid _testRunGuid;

        public TestStreamNameBuilder(Guid testRunGuid)
        {
            _testRunGuid = testRunGuid;
        }
        public string GenerateForAggregate(Type type, Guid id)
        {
            return $"{type.Name}-{id:N}{_testRunGuid:N}";
        }

        public string GenerateForCategory(Type type)
        {
            //mock category stream, can't use $ here
            return $"ce-{type.Name}{_testRunGuid:N}";
        }

        public string GenerateForEventType(string type)
        {
            //mock event type stream, can't use $ here
            return $"et-{type}{_testRunGuid:N}";
        }
    }
}
