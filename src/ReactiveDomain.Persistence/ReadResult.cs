using System;

namespace ReactiveDomain
{
    /// <summary>
    /// Represents the result of restoring a source of events from an event stream.
    /// </summary>
    public class ReadResult
    {
        /// <summary>
        /// Represents the fact the underlying stream was not found.
        /// </summary>
        public static readonly ReadResult NotFound = 
            new ReadResult(ReadResultState.NotFound, null);
        /// <summary>
        /// Represents the fact the underlying stream was deleted.
        /// </summary>
        public static readonly ReadResult Deleted = 
            new ReadResult(ReadResultState.Deleted, null);
        /// <summary>
        /// Represents the fact the underlying stream was found and captures the result of translating it into a source of events.
        /// </summary>
        public static ReadResult Found(IEventSource value) => 
            new ReadResult(ReadResultState.Found, value);

        private readonly IEventSource _value;

        private ReadResult(ReadResultState state, IEventSource value)
        {
            State = state;
            _value = value;
        }

        /// <summary>
        /// Discriminates whether the underlying stream was not found, deleted or found.
        /// </summary>
        public ReadResultState State { get; }

        /// <summary>
        /// The result of translating the underlying stream into a source of events.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the underlying stream was not found or deleted.</exception>
        public IEventSource Value
        {
            get
            {
                if (State == ReadResultState.NotFound)
                    throw new InvalidOperationException(
                        "There's no value when the underlying stream was not found.");

                if (State == ReadResultState.Deleted)
                    throw new InvalidOperationException(
                        "There's no value when the underlying stream was deleted.");

                // (State == LoadState.Found)
                return _value;
            }
        }

        private bool Equals(ReadResult other) => Equals(_value, other._value) && State == other.State;

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((ReadResult)obj);
        }

        public override int GetHashCode() => (_value?.GetHashCode() ?? 0) ^ (int)State;
    }
}
