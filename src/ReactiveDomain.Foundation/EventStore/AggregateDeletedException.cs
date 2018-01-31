using System;

// ReSharper disable  MemberCanBePrivate.Global
// ReSharper disable  NotAccessedField.Global
namespace ReactiveDomain.Foundation.EventStore
{
    public class AggregateDeletedException : Exception
    {

        public readonly Guid Id;
        public readonly Type Type;

        public AggregateDeletedException(Guid id, Type type) 
            : base(string.Format("Aggregate '{0}' (type {1}) was deleted.", id, type.Name))
        {
            Id = id;
            Type = type;
        }
    }
}