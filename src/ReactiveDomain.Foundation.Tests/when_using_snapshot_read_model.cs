using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;
using Xunit;

namespace ReactiveDomain.Foundation.Tests {
    // ReSharper disable once InconsistentNaming
    public class when_using_snapshot_read_model : IClassFixture<StreamStoreConnectionFixture> {
        private IListener GetListener() {
            return new SynchronizableStreamListener(
                nameof(when_using_read_model_base),
                _conn,
                _namer,
                _serializer,
                true);
        }

        private readonly IStreamStoreConnection _conn;
        private readonly IEventSerializer _serializer =
            new JsonMessageSerializer();
        private readonly IStreamNameBuilder _namer =
            new PrefixedCamelCaseStreamNameBuilder(nameof(when_using_snapshot_read_model));

        private readonly string _stream;
        private readonly Guid _aggId;

        private void AppendEvents(
            int numEventsToBeSent,
            IStreamStoreConnection conn,
            string streamName,
            int value) {
            for (int evtNumber = 0; evtNumber < numEventsToBeSent; evtNumber++) {
                var evt = new SnapReadModelTestEvent(evtNumber, value);
                conn.AppendToStream(streamName, ExpectedVersion.Any, null, _serializer.Serialize(evt));
            }
        }
        public when_using_snapshot_read_model(StreamStoreConnectionFixture fixture) {
            _conn = fixture.Connection;
            _conn.Connect();

            _aggId = Guid.NewGuid();
            _stream = _namer.GenerateForAggregate(typeof(SnapReadModelTestAggregate), _aggId);

            AppendEvents(10, _conn, _stream, 2);

        }
        [Fact]
        public void can_get_snapshot_from_read_model() {
            var rm = new TestSnapShotReadModel(_aggId, GetListener, null);
            AssertEx.IsOrBecomesTrue(() => rm.Count == 10);
            var snapshot = rm.GetState();

            Assert.Equal(nameof(TestSnapShotReadModel), snapshot.ModelName);
            Assert.Single(snapshot.Checkpoints);
            Assert.Equal(_stream, snapshot.Checkpoints[0].Item1);
            Assert.Equal(9, snapshot.Checkpoints[0].Item2);
            var state = snapshot.State as TestSnapShotReadModel.MyState;
            Assert.NotNull(state);
            Assert.Equal(10, state.Count);
            Assert.Equal(20, state.Sum);
        }
        [Fact]
        public void can_apply_snapshot_to_read_model() {
            var snapshot = new ReadModelState(
                                nameof(TestSnapShotReadModel),
                                new List<Tuple<string, long>> { new Tuple<string, long>(_stream, 9) },
                                new TestSnapShotReadModel.MyState { Count = 10, Sum = 20 });

            var rm = new TestSnapShotReadModel(_aggId, GetListener, snapshot);
            AssertEx.IsOrBecomesTrue(() => rm.Count == 10);
            AssertEx.IsOrBecomesTrue(() => rm.Sum == 20);
            AppendEvents(1, _conn, _stream, 5);
            AssertEx.IsOrBecomesTrue(() => rm.Count == 11, 1000);
            AssertEx.IsOrBecomesTrue(() => rm.Sum == 25);
        }
        [Fact]
        [SuppressMessage("ReSharper", "AccessToDisposedClosure")]
        public void can_snapshot_and_recover_read_model() {
            var rm = new TestSnapShotReadModel(_aggId, GetListener, null);
            AssertEx.IsOrBecomesTrue(() => rm.Count == 10);
            AssertEx.IsOrBecomesTrue(() => rm.Sum == 20);
            AppendEvents(1, _conn, _stream, 5);
            AssertEx.IsOrBecomesTrue(() => rm.Count == 11, 1000);
            AssertEx.IsOrBecomesTrue(() => rm.Sum == 25);
            var snap = rm.GetState();
            rm.Dispose();
            var rm2 = new TestSnapShotReadModel(_aggId, GetListener, snap);
            AssertEx.IsOrBecomesTrue(() => rm2.Count == 11, 1000);
            AssertEx.IsOrBecomesTrue(() => rm2.Sum == 25);
            AppendEvents(1, _conn, _stream, 5);
            AssertEx.IsOrBecomesTrue(() => rm2.Count == 12, 1000);
            AssertEx.IsOrBecomesTrue(() => rm2.Sum == 30);
        }
        [Fact]
        public void can_only_restore_once() {
            var rm = new TestSnapShotReadModel(_aggId, GetListener, null);
            // ReSharper disable once AccessToDisposedClosure
            AssertEx.IsOrBecomesTrue(() => rm.Count == 10);
            var state = rm.GetState();
            rm.Dispose();
            //create while forcing double restore
            Assert.Throws<InvalidOperationException>(() => new TestSnapShotReadModel(
                                                                    _aggId, 
                                                                    GetListener, 
                                                                    state,
                                                                    forceDoubleRestoreError:true));
        }
        [Fact]
        public void can_restore_state_without_checkpoints() {
            var snapshot = new ReadModelState(
                nameof(TestSnapShotReadModel),
                null, //no streams provided
                new TestSnapShotReadModel.MyState { Count = 10, Sum = 20 });

            var rm = new TestSnapShotReadModel(_aggId, GetListener, snapshot);
            AssertEx.IsOrBecomesTrue(() => rm.Count == 10);
            AssertEx.IsOrBecomesTrue(() => rm.Sum == 20);
            
            //n.b. will not start listening because no streams are provided
            //confirm not listening
            AppendEvents(1, _conn, _stream, 5);
            AssertEx.IsOrBecomesTrue(() => rm.Count == 10, 1000);
            AssertEx.IsOrBecomesTrue(() => rm.Sum == 20);

            //can manually start
            rm.Start<SnapReadModelTestAggregate>(_aggId, 9, true);
            AssertEx.IsOrBecomesTrue(() => rm.Count == 11, 1000);
            AssertEx.IsOrBecomesTrue(() => rm.Sum == 25);
            AppendEvents(1, _conn, _stream, 5);
            AssertEx.IsOrBecomesTrue(() => rm.Count == 12, 1000);
            AssertEx.IsOrBecomesTrue(() => rm.Sum == 30);

            //can still get complete snapshot
            var snap2 = rm.GetState();
            //works as expected
            var rm2 = new TestSnapShotReadModel(_aggId, GetListener, snap2);
            AssertEx.IsOrBecomesTrue(() => rm2.Count == 12, 1000);
            AssertEx.IsOrBecomesTrue(() => rm2.Sum == 30);
            AppendEvents(1, _conn, _stream, 5);
            AssertEx.IsOrBecomesTrue(() => rm2.Count == 13, 1000);
            AssertEx.IsOrBecomesTrue(() => rm2.Sum == 35);

        }
        public sealed class TestSnapShotReadModel :
            SnapshotReadModel,
            IHandle<SnapReadModelTestEvent> {
            public TestSnapShotReadModel(
                    Guid aggId,
                    Func<IListener> getListener,
                    ReadModelState snapshot,
                    bool forceDoubleRestoreError = false) :
                base(nameof(TestSnapShotReadModel), getListener) {
                // ReSharper disable once RedundantTypeArgumentsOfMethod
                EventStream.Subscribe<SnapReadModelTestEvent>(this);

                if (snapshot is null) {
                    Start<SnapReadModelTestAggregate>(aggId, null, true);
                }
                else {
                    Restore(snapshot);
                }
                if(forceDoubleRestoreError) {
                    Restore(snapshot);
                }
            }
            public long Sum { get; private set; }
            public long Count { get; private set; }
            void IHandle<SnapReadModelTestEvent>.Handle(SnapReadModelTestEvent @event) {
                Sum += @event.Value;
                Count++;
            }
            protected override void ApplyState(ReadModelState snapshot) {
                if (snapshot?.State == null) {
                    throw new ArgumentNullException(nameof(snapshot), $"Null State provided to {nameof(TestSnapShotReadModel)}");
                }
                if (!(snapshot.State is MyState)) {
                    throw new ArgumentException($"Unknown state object: Expected {nameof(MyState)}, got {snapshot.State.GetType().Name}");
                }
                var state = (MyState)snapshot.State;
                Count = state.Count;
                Sum = state.Sum;
            }

            public override ReadModelState GetState() {
                return new ReadModelState(
                              nameof(TestSnapShotReadModel),
                              GetCheckpoint(),
                              new MyState { Sum = Sum, Count = Count });
            }
            public class MyState {
                public long Sum { get; set; }
                public long Count { get; set; }
            }
        }
        public class SnapReadModelTestAggregate : EventDrivenStateMachine { }
        public class SnapReadModelTestEvent : Message {
            public readonly int Number;
            public readonly int Value;

            public SnapReadModelTestEvent(
                int number,
                int value
            ) {
                Number = number;
                Value = value;
            }
        }
    }
}
