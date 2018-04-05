using System;
using ReactiveDomain.Messaging.Messages;

namespace ReactiveDomain.Messaging
{
    /// <summary>
    /// A unique identifier that links a message to its correlated chain.
    /// </summary>
    /// <remarks>This is a struct and not a class since it should not be nullable.</remarks>
    public struct CorrelationId
    {
        /// <summary>
        /// The unique correlation ID
        /// </summary>
        public readonly Guid Id;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="source">The antecedent message.</param>
        public CorrelationId(ICorrelatedMessage source)
        {
            Id = source.CorrelationId;
        }

        /// <summary>
        /// Constructor for deserialization. This should NOT be used directly.
        /// </summary>
        public CorrelationId(Guid id)
        {
            Id = id;
        }

        /// <summary>
        /// Create a new CorrelationId.
        /// </summary>
        public static CorrelationId NewId()
        {
            return new CorrelationId(Guid.NewGuid());
        }

        /// <summary>
        /// Convert this to a Guid.
        /// </summary>
        /// <param name="corrId"></param>
        public static implicit operator Guid(CorrelationId corrId)
        {
            return corrId.Id;
        }

        public bool Equals(CorrelationId other)
        {
            return Id.Equals(other.Id);
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }
    }
}
