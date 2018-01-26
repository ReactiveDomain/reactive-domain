using System;
using ReactiveDomain;
using Xunit;

namespace ReactiveDomain
{
    namespace EventRecorderTests
    {
        public class InstanceWithoutRecords
        {
            private readonly EventRecorder _sut;

            public InstanceWithoutRecords()
            {
                _sut = new EventRecorder();
            }

            [Fact]
            public void ResetHasExpectedResult()
            {
                _sut.Reset();

                Assert.Equal(
                    new object[0],
                    _sut.RecordedEvents);
            }

            [Fact]
            public void RecordDoesNotAcceptNull()
            {
                Assert.Throws<ArgumentNullException>(
                    () => _sut.Record(null));
            }

            [Fact]
            public void RecordHasExpectedResult()
            {
                var @event = new LocalEvent();

                _sut.Record(@event);

                Assert.Equal(
                    new object[] { @event },
                    _sut.RecordedEvents);
            }

            [Fact]
            public void RecordedEventsReturnsExpectedValue()
            {
                Assert.Equal(new object[0], _sut.RecordedEvents);
            }

            [Fact]
            public void HasRecordedEventsReturnsExpectedValue()
            {
                Assert.False(_sut.HasRecordedEvents);
            }

            private class LocalEvent
            {
            }
        }

        public class InstanceWithRecords
        {
            private readonly EventRecorder _sut;
            private readonly LocalEvent _record1;
            private readonly LocalEvent _record2;

            public InstanceWithRecords()
            {
                _sut = new EventRecorder();

                _record1 = new LocalEvent();
                _record2 = new LocalEvent();

                _sut.Record(_record1);
                _sut.Record(_record2);
            }

            [Fact]
            public void ResetHasExpectedResult()
            {
                _sut.Reset();

                Assert.Equal(
                    new object[0],
                    _sut.RecordedEvents);
            }

            [Fact]
            public void RecordDoesNotAcceptNull()
            {
                Assert.Throws<ArgumentNullException>(
                    () => _sut.Record(null));
            }

            [Fact]
            public void RecordHasExpectedResult()
            {
                var @event = new LocalEvent();

                _sut.Record(@event);

                Assert.Equal(
                    new object[] { _record1, _record2, @event },
                    _sut.RecordedEvents);
            }

            [Fact]
            public void RecordedEventsReturnsExpectedValue()
            {
                Assert.Equal(new object[] { _record1, _record2 }, _sut.RecordedEvents);
            }

            [Fact]
            public void HasRecordedEventsReturnsExpectedValue()
            {
                Assert.True(_sut.HasRecordedEvents);
            }

            private class LocalEvent
            {
            }
        }

        public class ResetInstance
        {
            private readonly EventRecorder _sut;

            public ResetInstance()
            {
                _sut = new EventRecorder();

                var record1 = new LocalEvent();
                var record2 = new LocalEvent();

                _sut.Record(record1);
                _sut.Record(record2);
                _sut.Reset();
            }

            [Fact]
            public void ResetHasExpectedResult()
            {
                _sut.Reset();

                Assert.Equal(
                    new object[0],
                    _sut.RecordedEvents);
            }

            [Fact]
            public void RecordDoesNotAcceptNull()
            {
                Assert.Throws<ArgumentNullException>(
                    () => _sut.Record(null));
            }

            [Fact]
            public void RecordHasExpectedResult()
            {
                var @event = new LocalEvent();

                _sut.Record(@event);

                Assert.Equal(
                    new object[] { @event },
                    _sut.RecordedEvents);
            }

            [Fact]
            public void RecordedEventsReturnsExpectedValue()
            {
                Assert.Equal(new object[0], _sut.RecordedEvents);
            }

            [Fact]
            public void HasRecordedEventsReturnsExpectedValue()
            {
                Assert.False(_sut.HasRecordedEvents);
            }

            private class LocalEvent
            {
            }
        }
    }
}