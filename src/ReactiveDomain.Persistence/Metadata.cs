using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace ReactiveDomain
{
    
    public class Metadata : IEquatable<Metadata>, IEnumerable<Metadatum>
    {
        public static readonly Metadata None = new Metadata(new Metadatum[0]);

        private readonly Metadatum[] _metadata;
        private KeyValuePair<string, string>[] _pairs;

        public Metadata(Metadatum[] metadata)
        {
            _metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        }

        public Metadata With(Metadatum metadatum)
        {
            var copy = new Metadatum[_metadata.Length + 1];
            Array.Copy(_metadata, copy, _metadata.Length);
            copy[copy.Length - 1] = metadatum;
            return new Metadata(copy);
        }

        public Metadata With(Metadatum[] metadata)
        {
            if (metadata == null) throw new ArgumentNullException(nameof(metadata));
            var copy = new Metadatum[_metadata.Length + metadata.Length];
            Array.Copy(_metadata, copy, _metadata.Length);
            Array.Copy(metadata, 0, copy, _metadata.Length, metadata.Length);
            return new Metadata(copy);
        }

        public Metadata With(string name, string value)
        {
            return With(new Metadatum(name, value));
        }

        public Metadata Without(Metadatum metadatum)
        {
            var copy = Array.FindAll(_metadata, 
                candidate => !candidate.Equals(metadatum));
            return new Metadata(copy);
        }

        public Metadata Without(Metadatum[] metadata)
        {
            if (metadata == null)
                throw new ArgumentNullException(nameof(metadata));

            var copy = Array.FindAll(_metadata, 
                candidate => !Array.Exists(metadata, 
                    removable => removable.Equals(candidate)));
            return new Metadata(copy);
        }

        public Metadata Without(string name)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));
                
            var copy = Array.FindAll(_metadata,
                candidate => candidate.Name != name);
            return new Metadata(copy);
        }

        public KeyValuePair<string, string>[] ToKeyValuePairs()
        {
            return _pairs ?? (_pairs = _metadata.Select(metadatum => metadatum.ToKeyValuePair()).ToArray());
        }

        public bool Equals(Metadata other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return _metadata.SequenceEqual(other._metadata);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((Metadata)obj);
        }

        public override int GetHashCode()
        {
            return _metadata.Aggregate(0, (current, next) => current ^ next.GetHashCode());
        }

        public IEnumerator<Metadatum> GetEnumerator()
        {
            return ((IEnumerable<Metadatum>)_metadata).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}