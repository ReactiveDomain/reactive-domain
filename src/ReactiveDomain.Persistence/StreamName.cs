using System;
using System.Diagnostics.Contracts;

namespace ReactiveDomain
{
    public struct StreamName : IEquatable<StreamName>
    {
        private readonly string _value;

        public StreamName(string value)
        {
            _value = value ?? throw new ArgumentNullException(nameof(value));
        }

        [Pure]
        public StreamName WithSuffix(string suffix)
        {
            return _value == null ? new StreamName(suffix) : new StreamName(_value + suffix);
        }

        [Pure]
        public StreamName WithPrefix(string prefix)
        {
            return _value == null ? new StreamName(prefix) : new StreamName(prefix + _value);
        }

        [Pure]
        public bool StartsWith(string prefix)
        {
            return _value != null && _value.StartsWith(prefix);
        }

        [Pure]
        public bool EndsWith(string suffix)
        {
            return _value != null && _value.EndsWith(suffix);
        }

        [Pure]
        public StreamName WithoutPrefix(string prefix)
        {
            if (StartsWith(prefix))
            {
                return new StreamName(_value.Substring(prefix.Length));
            }
            return this;
        }

        [Pure]
        public StreamName WithoutSuffix(string suffix)
        {
            if (EndsWith(suffix))
            {
                return new StreamName(_value.Substring(0, _value.Length - suffix.Length));
            }
            return this;
        }

        public bool Equals(StreamName other)
        {
            return string.Equals(_value, other._value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is StreamName && Equals((StreamName)obj);
        }

        public override int GetHashCode()
        {
            return _value == null ? 0 : _value.GetHashCode();
        }

        public override string ToString()
        {
            return _value ?? "";
        }

        public static implicit operator string(StreamName instance)
        {
            return instance.ToString();
        }
    }
}