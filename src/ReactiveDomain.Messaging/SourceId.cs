using System;
using ReactiveDomain.Messaging.Messages;

namespace ReactiveDomain.Messaging
{
    /// <summary>
    /// A unique identifier that links a correlated message to its antecedent.
    /// </summary>
    /// <remarks>This is a struct and not a class since it should not be nullable.</remarks>
    public struct SourceId
    {
        /// <summary>
        /// The unique source ID
        /// </summary>
        public readonly Guid Id;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="source">The antecedent message.</param>
        public SourceId(ICorrelatedMessage source)
        : this(source.MsgId)
        {
        }

        /// <summary>
        /// Constructor for deserialization. This should NOT be used directly.
        /// </summary>
        public SourceId(Guid id)
        {
            Id = id;
        }

        public static SourceId NullSourceId()
        {
            return new SourceId(Guid.Empty);
        }

        public static implicit operator Guid(SourceId srcId)
        {
            return srcId.Id;
        }

        public bool Equals(SourceId other)
        {
            return Id.Equals(other.Id);
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }
    }
}
