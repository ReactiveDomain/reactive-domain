using System;

namespace ReactiveDomain
{
    public struct SliceSize : IEquatable<SliceSize>
    {
        private readonly int _value;

        public SliceSize(int value)
        {
            if(value < 1)
                throw new ArgumentOutOfRangeException(nameof(value), value, "The slice size value needs to be 1 or greater.");

            _value = value;
        }

        public bool Equals(SliceSize other)
        {
            return _value == other._value;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is SliceSize && Equals((SliceSize)obj);
        }

        public override int GetHashCode()
        {
            return _value;
        }

        public int ToInt32()
        {
            return _value;
        }

        public static implicit operator int(SliceSize instance)
        {
            return instance._value;
        }
    }
}