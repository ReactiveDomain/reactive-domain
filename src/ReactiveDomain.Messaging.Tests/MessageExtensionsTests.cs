using Xunit;

namespace ReactiveDomain.Messaging.Tests
{
    public class MessageExtensionsTests
    {
        [Fact]
        public void can_write_and_read_metadatum()
        {
            var metadatum = new CustomMetadata { Data = "Test" };
            var message = new TestEvent();
            message.WriteMetadatum(metadatum);

            var md = message.ReadMetadatum<CustomMetadata>();
            Assert.Equal(metadatum.Data, md.Data);
        }

        [Fact]
        public void read_metadatum_throws_if_not_found()
        {
            var message = new TestEvent();
            Assert.Throws<MetadatumNotFoundException>(() => message.ReadMetadatum<CustomMetadata>());
        }

        [Fact]
        public void can_try_read_metadatum()
        {
            var metadatum = new CustomMetadata { Data = "Test" };
            var message = new TestEvent();
            message.WriteMetadatum(metadatum);

            Assert.True(message.TryReadMetadatum<CustomMetadata>(out var md));
            Assert.Equal(metadatum.Data, md.Data);
        }

        [Fact]
        public void try_read_metadatum_reports_if_not_found()
        {
            var message = new TestEvent();
            Assert.False(message.TryReadMetadatum<CustomMetadata>(out var md));
            Assert.Equal(default, md);
        }

        public class TestEvent : Event { }

        public class CustomMetadata
        {
            public string Data;
        }
    }
}
