using ReactiveDomain.Foundation;
using Xunit;

namespace ReactiveDomain.Persistence.Dapper.Tests
{
    public class InMemoryCheckpointStoreTests
    {
        private readonly InMemoryCheckpointStore _store;

        public InMemoryCheckpointStoreTests()
        {
            _store = new InMemoryCheckpointStore();
        }

        [Fact]
        public void GetCheckpoint_returns_null_for_unknown_projection()
        {
            var result = _store.GetCheckpoint("unknown");

            Assert.Null(result);
        }

        [Fact]
        public void SaveCheckpoint_stores_position()
        {
            _store.SaveCheckpoint("TestProjection", 42);

            Assert.Equal(42, _store.GetCheckpoint("TestProjection"));
        }

        [Fact]
        public void SaveCheckpoint_overwrites_existing()
        {
            _store.SaveCheckpoint("TestProjection", 10);
            _store.SaveCheckpoint("TestProjection", 20);

            Assert.Equal(20, _store.GetCheckpoint("TestProjection"));
        }

        [Fact]
        public void Multiple_projections_are_independent()
        {
            _store.SaveCheckpoint("Projection1", 100);
            _store.SaveCheckpoint("Projection2", 200);

            Assert.Equal(100, _store.GetCheckpoint("Projection1"));
            Assert.Equal(200, _store.GetCheckpoint("Projection2"));
        }

        [Fact]
        public void GetAllCheckpoints_returns_all_stored()
        {
            _store.SaveCheckpoint("A", 1);
            _store.SaveCheckpoint("B", 2);
            _store.SaveCheckpoint("C", 3);

            var all = _store.GetAllCheckpoints();

            Assert.Equal(3, all.Count);
            Assert.Equal(1, all["A"]);
            Assert.Equal(2, all["B"]);
            Assert.Equal(3, all["C"]);
        }

        [Fact]
        public void Clear_removes_all_checkpoints()
        {
            _store.SaveCheckpoint("A", 1);
            _store.SaveCheckpoint("B", 2);

            _store.Clear();

            Assert.Null(_store.GetCheckpoint("A"));
            Assert.Null(_store.GetCheckpoint("B"));
            Assert.Empty(_store.GetAllCheckpoints());
        }
    }
}
