using System;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable NotAccessedField.Global
// ReSharper disable once CheckNamespace
namespace ReactiveDomain.Foundation
{
    public class AggregateVersionException : Exception
    {
        public readonly Guid Id;
        public readonly Type Type;
        public readonly long AggregateVersion;
        public readonly long RequestedVersion;
        
        public AggregateVersionException(Guid id, Type type, long aggregateVersion, long requestedVersion)
            : base(string.Format("Requested version {2} of aggregate '{0}' (type {1}) - aggregate version is {3}", id, type.Name, requestedVersion, aggregateVersion))
        {
            Id = id;
            Type = type;
            AggregateVersion = aggregateVersion;
            RequestedVersion = requestedVersion;
        }
    }
}