using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

// ReSharper disable UnusedParameter.Local

namespace ReactiveDomain.Testing.EventStore;

public class StreamReaderTests : IClassFixture<StreamStoreConnectionFixture>, IHandle<Event> {
    private readonly List<IStreamStoreConnection> _stores = [];
    private readonly IEventSerializer _serializer = new JsonMessageSerializer();
    private readonly string _streamName;
    private readonly IStreamNameBuilder _streamNameBuilder;
    private long _count;
    private readonly int NUM_OF_EVENTS = 10;
    private readonly ITestOutputHelper _toh;
    private Action<IMessage> _gotEvent;

    public StreamReaderTests(ITestOutputHelper toh, StreamStoreConnectionFixture fixture) {
        _toh = toh;
        _streamNameBuilder = new PrefixedCamelCaseStreamNameBuilder("UnitTest");
        var mockStreamStore = new MockStreamStoreConnection("Test-" + Guid.NewGuid());
        mockStreamStore.Connect();

        _stores.Add(mockStreamStore);
        _stores.Add(fixture.Connection);

        _streamName = _streamNameBuilder.GenerateForAggregate(typeof(TestAggregate), Guid.NewGuid());


        foreach (var store in _stores) {
            AppendEvents(NUM_OF_EVENTS, store, _streamName);
        }
    }

    private void AppendEvents(int numEventsToBeSent, IStreamStoreConnection conn, string streamName) {
        _toh.WriteLine(
            $"Appending {numEventsToBeSent} events to stream \"{streamName}\" with connection {conn.ConnectionName}");

        for (int evtNumber = 0; evtNumber < numEventsToBeSent; evtNumber++) {
            var evt = new ReadTestEvent(evtNumber);
            conn.AppendToStream(streamName, ExpectedVersion.Any, null, _serializer.Serialize(evt));
        }
    }

    private void AppendEventArray(int numEventsToBeSent, IStreamStoreConnection conn, string streamName) {
        _toh.WriteLine(
            $"Appending {numEventsToBeSent} events to stream \"{streamName}\" with connection {conn.ConnectionName}");

        var events = new IMessage[numEventsToBeSent];
        for (int evtNumber = 0; evtNumber < numEventsToBeSent; evtNumber++) {
            events[evtNumber] = new ReadTestEvent(evtNumber);
        }

        conn.AppendToStream(streamName, ExpectedVersion.Any, null,
            events.Select(x => _serializer.Serialize(x)).ToArray());
    }
    [Fact]
    public void can_read_stream_forward() {
        foreach (var conn in _stores) {
            _count = 0;
            var reader = new StreamReader("TestReader", conn, _streamNameBuilder, _serializer, evt => { if (evt is Event @event) { this.Handle(@event); } });

            // forward 1 from beginning
            _count = 0;
            Assert.Null(reader.Position);
            reader.Read(_streamName, () => true, count: 1);
            Assert.Equal(1, _count);
            Assert.Equal(0, reader.Position);

            // forward all
            _count = 0;
            reader.Read(_streamName, () => true);
            Assert.Equal(NUM_OF_EVENTS, _count);
        }
    }

    [Fact]
    public void can_read_stream_forward_from_position() {
        foreach (var conn in _stores) {
            _count = 0;
            var reader = new StreamReader("TestReader", conn, _streamNameBuilder, _serializer, evt => { if (evt is Event @event) { Handle(@event); } });
            var position = NUM_OF_EVENTS / 2; // zero-based position is the number of the last received event

            reader.Read(_streamName, () => true, position);

            Assert.Equal(NUM_OF_EVENTS - (position + 1), _count); // events skipped: 0...P; events read: P+1...N
        }
    }

    [Fact]
    public void can_read_zero_events_forward_from_position() {
        foreach (var conn in _stores) {
            _count = 0;
            var reader = new StreamReader("TestReader", conn, _streamNameBuilder, _serializer, evt => { if (evt is Event @event) { Handle(@event); } });


            reader.Read(_streamName, () => true, NUM_OF_EVENTS);

            Assert.Equal(0, _count);
            Assert.Throws<ArgumentOutOfRangeException>(() => reader.Read(_streamName, () => true, checkpoint: -10, readBackwards: true));

        }
    }


    [Fact]
    public void no_read_returns_false() {
        foreach (var conn in _stores) {
            _count = 0;
            var reader = new StreamReader("TestReader", conn, _streamNameBuilder, _serializer, evt => { if (evt is Event @event) { Handle(@event); } });


            Assert.False(reader.Read($"missing_stream-{Guid.NewGuid():N}", () => true));

            var deleteStream = $"deleteStream-{Guid.NewGuid().ToString()}";
            AppendEvents(1, conn, deleteStream);
            conn.DeleteStream(deleteStream, ExpectedVersion.Any);
            Assert.False(reader.Read(deleteStream, () => true));

            Assert.False(reader.Read(_streamName, () => true, NUM_OF_EVENTS + 1, 1));

        }
    }

    [Fact]
    public void can_read_forward_updating_stream() {
        foreach (var conn in _stores) {
            _count = 0;
            var reader = new StreamReader("TestReader", conn, _streamNameBuilder, _serializer, evt => { if (evt is Event @event) { Handle(@event); } });

            Parallel.Invoke(
                () => {
                    for (int chunkNum = 0; chunkNum < 10; chunkNum++)
                        AppendEventArray(NUM_OF_EVENTS, conn, _streamName);
                },
                () => {
                    Thread.Sleep(10); // to make that sure appends are started
                    reader.Read(_streamName, () => true);
                });

            _toh.WriteLine($"Read events: {_count}");
            Assert.Equal(0, _count % NUM_OF_EVENTS);
        }
    }

    [Fact]
    public void can_read_stream_backward() {
        foreach (var conn in _stores) {
            _count = 0;
            var reader = new StreamReader("TestReader", conn, _streamNameBuilder, _serializer, evt => { if (evt is Event @event) { Handle(@event); } });


            reader.Read(_streamName, () => true, readBackwards: true);

            Assert.Equal(NUM_OF_EVENTS, _count);
        }
    }

    [Fact]
    public void can_read_stream_backward_from_position() {
        foreach (var conn in _stores) {
            _count = 0;
            var reader = new StreamReader("TestReader", conn, _streamNameBuilder, _serializer, evt => { if (evt is Event @event) { Handle(@event); } });

            var position = NUM_OF_EVENTS / 2;

            reader.Read(_streamName, () => true, position, readBackwards: true);

            Assert.Equal(position, _count); // events from position: N-1, N-2...0 => N events
            Assert.Throws<ArgumentOutOfRangeException>(() => reader.Read(_streamName, () => true, checkpoint: -10, readBackwards: true));

        }
    }


    [Fact]
    public void can_read_stream_backward_multiple_slices() {
        foreach (var conn in _stores) {
            _count = 0;
            const int totalEvents = 10010;
            var longStreamName = _streamNameBuilder.GenerateForAggregate(typeof(TestAggregate), Guid.NewGuid());
            AppendEventArray(totalEvents, conn, longStreamName);
            var reader = new StreamReader("TestReader", conn, _streamNameBuilder, _serializer, evt => { if (evt is Event @event) { Handle(@event); } });


            var events = new List<ReadTestEvent>(totalEvents);
            _gotEvent = evt => { events.Add(evt as ReadTestEvent); };
            reader.Read(longStreamName, () => true, readBackwards: true);

            Assert.Equal(totalEvents, _count);
            var expectedNum = totalEvents - 1;
            foreach (var evt in events) {
                Assert.Equal(expectedNum, evt.MessageNumber);
                expectedNum--;
            }
        }
    }

    [Fact]
    public void can_cancel_long_read() {
        foreach (var conn in _stores) {
            _count = 0;
            var reader = new StreamReader("TestReader", conn, _streamNameBuilder, _serializer, evt => { if (evt is Event @event) { Handle(@event); } });

            var longStreamName = _streamNameBuilder.GenerateForAggregate(typeof(TestAggregate), Guid.NewGuid());

            const int manyEvents = 1000;

            AppendEventArray(manyEvents, conn, longStreamName);

            _gotEvent = e => {
                if (_count == 100) reader.Cancel();
                Thread.Sleep(10);
            };

            // forward
            reader.Read(longStreamName, () => true);

            _toh.WriteLine($"Read events: {_count} out of {manyEvents}, cancelled >= #100");
            Assert.Equal(101, _count); // counter increased after cancellation  - expected 101

            // reset
            _count = 0;
            // backward
            reader.Read(longStreamName, () => true, readBackwards: true);

            _toh.WriteLine($"Read events: {_count} out of {manyEvents}, cancelled >= #100");
            Assert.Equal(101, _count);
        }
    }

    [Fact]
    public void can_read_count_events() {
        foreach (var conn in _stores) {
            var reader = new StreamReader("TestReader", conn, _streamNameBuilder, _serializer, evt => { if (evt is Event @event) { Handle(@event); } });

            var longStreamName = _streamNameBuilder.GenerateForAggregate(typeof(TestAggregate), Guid.NewGuid());

            const int manyEvents = 1550;
            AppendEventArray(manyEvents, conn, longStreamName);



            // forward from 0
            _count = 0;
            reader.Read(longStreamName, () => true, count: 10);
            Assert.Equal(10, _count);

            // forward 2000
            _count = 0;
            reader.Read(longStreamName, () => true, count: 2 * manyEvents);
            Assert.Equal(manyEvents, _count);

            // forward 10 from 1000
            _count = 0;
            reader.Read(longStreamName, () => true, checkpoint: 1000, count: 10);
            Assert.Equal(10, _count);

            // last 10
            _count = 0;
            reader.Read(longStreamName, () => true, count: 10, readBackwards: true);
            Assert.Equal(10, _count);

            // last 2000 out of 1550
            _count = 0;
            reader.Read(longStreamName, () => true, count: 2 * manyEvents, readBackwards: true);
            Assert.Equal(manyEvents, _count);

            // backward 10 from 1000
            _count = 0;
            reader.Read(longStreamName, () => true, checkpoint: 1000, count: 10, readBackwards: true);
            Assert.Equal(10, _count);

            // non-positive count
            Assert.Throws<ArgumentOutOfRangeException>(() => reader.Read(longStreamName, () => true, count: -10, readBackwards: true));
            Assert.Throws<ArgumentOutOfRangeException>(() => reader.Read(longStreamName, () => true, count: -10));
            Assert.Throws<ArgumentOutOfRangeException>(() => reader.Read(longStreamName, () => true, checkpoint: 1000, count: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => reader.Read(longStreamName, () => true, checkpoint: 1000, count: 0, readBackwards: true));
        }
    }
    [Fact]
    public void can_read_from_projection() {
        foreach (var conn in _stores) {
            _count = 0;
            var reader = new StreamReader("TestReader", conn, _streamNameBuilder, _serializer, evt => { if (evt is Event @event) { Handle(@event); } });

            var streamName2 = _streamNameBuilder.GenerateForAggregate(typeof(TestAggregate), Guid.NewGuid());
            var categoryStream = _streamNameBuilder.GenerateForCategory(typeof(TestAggregate));
            var typeStream = _streamNameBuilder.GenerateForEventType(nameof(ReadTestEvent));

            AppendEventArray(NUM_OF_EVENTS, conn, streamName2);
            Thread.Sleep(100);


            // forward 1 from beginning
            _count = 0;
            Assert.Null(reader.Position);
            reader.Read(categoryStream, () => true, count: 1);
            Assert.Equal(1, _count);
            Assert.Equal(0, reader.Position);

            // forward 2 from beginning
            _count = 0;
            reader.Read(categoryStream, () => true, count: 2);
            Assert.Equal(2, _count);
            Assert.Equal(1, reader.Position);


            // forward 2 from 12
            _count = 0;
            var lastReceived = 12; // zero-based number of the last received event
            reader.Read(categoryStream, () => true, checkpoint: lastReceived, count: 2);
            Assert.Equal(2, _count);
            Assert.Equal(lastReceived + 2, reader.Position);

            // forward 10 from 5
            _count = 0;
            lastReceived = 5;
            reader.Read(categoryStream, () => true, checkpoint: lastReceived, count: 10);
            Assert.Equal(10, _count);
            Assert.Equal(lastReceived + 10, reader.Position);

            // backward 5 from 10
            _count = 0;
            lastReceived = 10;
            reader.Read(categoryStream, () => true, checkpoint: 10, count: 5, readBackwards: true);
            Assert.Equal(5, _count);
            Assert.Equal(lastReceived - 5, reader.Position);


            // backward 10 from 5
            _count = 0;
            lastReceived = 5;
            reader.Read(categoryStream, () => true, checkpoint: lastReceived, count: 10, readBackwards: true);
            Assert.Equal(5, _count);
            Assert.Equal(0, reader.Position);
        }
    }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "xUnit1013:Public method should be marked as test", Justification = "Interface required for fixture.")]
    public void Handle(Event message) {
        _gotEvent?.Invoke(message);
        Interlocked.Increment(ref _count);
    }

    public record ReadTestEvent(int MessageNumber) : Event;
}