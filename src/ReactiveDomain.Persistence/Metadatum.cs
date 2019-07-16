using System;
using System.Collections.Generic;

namespace ReactiveDomain
{
    //Useful to enforce length constraints on keys and values.
    //such that people don't go overboard and you can reuse the 
    //same keys/values across e.g. AWS services & EventStore
    public struct Metadatum : IEquatable<Metadatum>
    {
        private readonly KeyValuePair<string, string> _pair;

        public string Name => _pair.Key;
        public string Value => _pair.Value;

        public Metadatum(string name, string value)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            if (value == null) throw new ArgumentNullException(nameof(value));

            // Rationale: converting to a pair is the most common operation anyway.
            _pair = new KeyValuePair<string, string>(name, value);
        }

        public KeyValuePair<string, string> ToKeyValuePair()
        {
            return _pair;
        }

        public bool Equals(Metadatum other)
        {
            return _pair.Equals(other._pair);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is Metadatum && Equals((Metadatum)obj);
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode() ^ Value.GetHashCode();
        }
    }
}