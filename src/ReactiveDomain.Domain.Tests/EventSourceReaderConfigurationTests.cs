using System;
using ReactiveDomain;
using Xunit;

namespace ReactiveDomain
{
    public class EventSourceReaderConfigurationTests
    {
        [Fact]
        public void ConverterCanNotBeNull()
        {
            Assert.Throws<ArgumentNullException>(
                () => new EventSourceReaderConfiguration(
                    null,
                    () => null,
                    new SliceSize(1)));
            
        }

        [Fact]
        public void ConverterReturnsExpectedResult()
        {
            StreamNameConverter converter = name => name;

            var sut = new EventSourceReaderConfiguration(
                converter,
                () => null,
                new SliceSize(1));

            Assert.Same(converter, sut.Converter);
        }

        [Fact]
        public void TranslatorFactoryCanNotBeNull()
        {
            Assert.Throws<ArgumentNullException>(
                () => new EventSourceReaderConfiguration(
                    StreamNameConversions.PassThru,
                    null,
                    new SliceSize(1)));
        }

        [Fact]
        public void TranslatorFactoryReturnsExpectedResult()
        {
            Func<IStreamEventsSliceTranslator> factory = () => null;

            var sut = new EventSourceReaderConfiguration(
                StreamNameConversions.PassThru, 
                factory, 
                new SliceSize(1));

            Assert.Same(factory, sut.TranslatorFactory);
        }

        [Fact]
        public void SliceSizeReturnsExpectedResult()
        {
            var size = new SliceSize(2);
            var sut = new EventSourceReaderConfiguration(
                StreamNameConversions.PassThru, 
                () => null,
                size);

            Assert.Equal(size, sut.SliceSize);
        }
    }
}
