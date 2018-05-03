using System;
using Newtonsoft.Json;

namespace ReactiveDomain.Messaging
{
    /// <summary>
    /// A unique identifier that links a correlated message to its antecedent.
    /// </summary>
    /// <remarks>This is a struct and not a class since it should not be nullable.</remarks>
    public struct SourceId
    {
        /// <summary>
        /// The unique source ID.
        /// </summary>
        public readonly Guid Id;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="source">The antecedent message.</param>
        public SourceId(CorrelatedMessage source)
        : this(source.MsgId)
        {
        }

        private SourceId(Guid id)
        {
            Id = id;
        }

        /// <summary>
        /// Create a SourceId with no antecedent message.
        /// </summary>
        /// <returns>A SourceId with Guid.Empty as its Id.</returns>
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

        public class SourceIdGuidConverter : JsonConverter
        {
            public override void WriteJson(JsonWriter writer,
                                           object value,
                                           JsonSerializer serializer)
            {
                writer.WriteValue((SourceId)value);
            }

            public override object ReadJson(JsonReader reader,
                                            Type objectType,
                                            object existingValue,
                                            JsonSerializer serializer)
            {
                return new SourceId(Guid.Parse(reader.Value.ToString()));
            }

            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(SourceId);
            }
        }
    }
    }
