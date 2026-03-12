using System;
using System.Linq;
using ReactiveDomain.Foundation;
using Xunit;

namespace ReactiveDomain.Persistence.Dapper.Tests
{
    public class InMemoryReadModelStoreTests
    {
        private readonly InMemoryReadModelStore<Guid, TestEntity> _store;

        public InMemoryReadModelStoreTests()
        {
            _store = new InMemoryReadModelStore<Guid, TestEntity>();
        }

        [Fact]
        public void Insert_adds_entity()
        {
            var id = Guid.NewGuid();
            var entity = new TestEntity { Id = id, Name = "Test" };

            _store.Insert(id, entity);

            Assert.Equal(1, _store.Count);
            Assert.Equal("Test", _store.GetById(id).Name);
        }

        [Fact]
        public void Insert_does_not_overwrite_existing()
        {
            var id = Guid.NewGuid();
            var entity1 = new TestEntity { Id = id, Name = "First" };
            var entity2 = new TestEntity { Id = id, Name = "Second" };

            _store.Insert(id, entity1);
            _store.Insert(id, entity2);

            Assert.Equal(1, _store.Count);
            Assert.Equal("First", _store.GetById(id).Name);
        }

        [Fact]
        public void Update_modifies_existing_entity()
        {
            var id = Guid.NewGuid();
            var entity = new TestEntity { Id = id, Name = "Original", Value = 10 };
            _store.Insert(id, entity);

            _store.Update(id, e => new TestEntity { Id = e.Id, Name = "Updated", Value = e.Value + 5 });

            var result = _store.GetById(id);
            Assert.Equal("Updated", result.Name);
            Assert.Equal(15, result.Value);
        }

        [Fact]
        public void Update_does_nothing_for_nonexistent_entity()
        {
            var id = Guid.NewGuid();

            _store.Update(id, e => new TestEntity { Id = e.Id, Name = "Updated" });

            Assert.Equal(0, _store.Count);
        }

        [Fact]
        public void Upsert_inserts_new_entity()
        {
            var id = Guid.NewGuid();
            var entity = new TestEntity { Id = id, Name = "New" };

            _store.Upsert(id, entity);

            Assert.Equal(1, _store.Count);
            Assert.Equal("New", _store.GetById(id).Name);
        }

        [Fact]
        public void Upsert_overwrites_existing_entity()
        {
            var id = Guid.NewGuid();
            var entity1 = new TestEntity { Id = id, Name = "First" };
            var entity2 = new TestEntity { Id = id, Name = "Second" };

            _store.Insert(id, entity1);
            _store.Upsert(id, entity2);

            Assert.Equal(1, _store.Count);
            Assert.Equal("Second", _store.GetById(id).Name);
        }

        [Fact]
        public void GetById_returns_null_for_nonexistent()
        {
            var result = _store.GetById(Guid.NewGuid());

            Assert.Null(result);
        }

        [Fact]
        public void GetAll_returns_all_entities()
        {
            var id1 = Guid.NewGuid();
            var id2 = Guid.NewGuid();
            _store.Insert(id1, new TestEntity { Id = id1, Name = "One" });
            _store.Insert(id2, new TestEntity { Id = id2, Name = "Two" });

            var all = _store.GetAll();

            Assert.Equal(2, all.Count);
        }

        [Fact]
        public void GetWhere_filters_by_predicate()
        {
            var id1 = Guid.NewGuid();
            var id2 = Guid.NewGuid();
            var id3 = Guid.NewGuid();
            _store.Insert(id1, new TestEntity { Id = id1, Name = "Alpha", Value = 10 });
            _store.Insert(id2, new TestEntity { Id = id2, Name = "Beta", Value = 20 });
            _store.Insert(id3, new TestEntity { Id = id3, Name = "Gamma", Value = 30 });

            var result = _store.GetWhere(e => e.Value > 15);

            Assert.Equal(2, result.Count);
            Assert.All(result, e => Assert.True(e.Value > 15));
        }

        [Fact]
        public void Clear_removes_all_entities()
        {
            _store.Insert(Guid.NewGuid(), new TestEntity { Name = "One" });
            _store.Insert(Guid.NewGuid(), new TestEntity { Name = "Two" });

            _store.Clear();

            Assert.Equal(0, _store.Count);
        }

        public class TestEntity
        {
            public Guid Id { get; set; }
            public string Name { get; set; }
            public int Value { get; set; }
        }
    }
}
