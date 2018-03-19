using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace ReactiveDomain.Foundation.EventStore
{
    public class AggregateFileRepository : IRepository
    {
        private readonly DirectoryInfo _folder;
        private readonly Func<Type, Guid, string> _aggregateIdToFileName;
        private readonly Func<Type, Guid, string> _fileFullName;
        private static readonly JsonSerializerSettings SerializerSettings;



        static AggregateFileRepository()
        {
            SerializerSettings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None };
        }
        public AggregateFileRepository(DirectoryInfo repository)
            : this(repository, (t, g) => $"{char.ToLower(t.Name[0]) + t.Name.Substring(1)}-{g.ToString("N")}.agg")
        {
        }

        public AggregateFileRepository(DirectoryInfo folder, Func<Type, Guid, string> aggregateIdToFileName)
        {
            _folder = folder;
            _aggregateIdToFileName = aggregateIdToFileName;
            _fileFullName = (t, g) => Path.Combine(_folder.FullName, _aggregateIdToFileName(t, g));
        }
        public TAggregate GetById<TAggregate>(Guid id, int version) where TAggregate : class, IEventSource
        {
            return GetById<TAggregate>(id);
        }

        public bool TryGetById<TAggregate>(Guid id, out TAggregate aggregate) where TAggregate : class, IEventSource
        {
            throw new NotImplementedException();
        }

        public bool TryGetById<TAggregate>(Guid id, int version, out TAggregate aggregate) where TAggregate : class, IEventSource
        {
            throw new NotImplementedException();
        }

        public TAggregate GetById<TAggregate>(Guid id) where TAggregate : class, IEventSource
        {
            var fileText = File.ReadAllText(_fileFullName(typeof(TAggregate), id));
            return (TAggregate)JsonConvert.DeserializeObject(fileText, typeof(TAggregate), SerializerSettings);
        }

        public DateTime GetCreationTimeById<TAggregate>(Guid id) where TAggregate : class, IEventSource
        {
            return File.GetCreationTime(_fileFullName(typeof(TAggregate), id));
        }

        public DateTime GetLastModificationTimeById<TAggregate>(Guid id) where TAggregate : class, IEventSource
        {
            return File.GetLastWriteTime(_fileFullName(typeof(TAggregate), id));
        }

        public IEnumerable<Tuple<Type, IEnumerable<Guid>>> EnumerateAggregateInstances()
        {
            return EnumerateAggregateTypes()
                .Select(type => new Tuple<Type, IEnumerable<Guid>>(
                    type,
                    EnumerateAggregateInstancesFor(type))
                );
        }

        public IEnumerable<Type> EnumerateAggregateTypes()
        {
            return _folder
                .GetFiles("*.agg")
                .Select(file => new String(file.Name.TakeWhile(c => c != '-').ToArray()))
                .Select(Type.GetType);
        }

        public IEnumerable<Guid> EnumerateAggregateInstancesFor<TAggregate>() where TAggregate : class, IEventSource
        {
            return EnumerateAggregateInstancesFor(typeof(TAggregate));
        }
        private IEnumerable<Guid> EnumerateAggregateInstancesFor(Type type)
        {
            var files = _folder.GetFiles($"{type.Name}-*.agg");
            var ids = files.Select(f => new string(f.Name.
                                                        SkipWhile(c => c != '-').
                                                        Skip(1).
                                                        TakeWhile(c => c != '.').ToArray()
                                                  )
                                    ).ToList();
            return ids.Select(id => Guid.ParseExact(id, "N"));

        }

        public void Save(IEventSource aggregate, Action<IDictionary<string, object>> updateHeaders = null)
        {
            var jsonText = JsonConvert.SerializeObject(aggregate, SerializerSettings);
            File.WriteAllText(
                _fileFullName(aggregate.GetType(), aggregate.Id),
                jsonText
                );
        }
    }
}