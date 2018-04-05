using System;
using System.Security.Cryptography;

namespace ReactiveDomain
{
    public class NameBasedGuidGenerator
    {
        private static readonly Tuple<int, int>[] ByteOrderPairsToSwap = {
            Tuple.Create(0, 3),
            Tuple.Create(1, 2),
            Tuple.Create(4, 5),
            Tuple.Create(6, 7)
        };

        private readonly byte[] _namespace;

        public NameBasedGuidGenerator(Guid @namespace)
        {
            _namespace = @namespace.ToByteArray();
            SwapPairs(_namespace, ByteOrderPairsToSwap);
        }

        public Guid Create(byte[] input)
        {
            byte[] hash;
            using (var algorithm = SHA1.Create())
            {
                algorithm.TransformBlock(_namespace, 0, _namespace.Length, null, 0);
                algorithm.TransformFinalBlock(input, 0, input.Length);
                hash = algorithm.Hash;
            }

            var buffer = new byte[16];
            Array.Copy(hash, 0, buffer, 0, 16);

            buffer[6] = (byte)((buffer[6] & 0x0F) | (5 << 4));
            buffer[8] = (byte)((buffer[8] & 0x3F) | 0x80);

            SwapPairs(buffer, ByteOrderPairsToSwap);
            return new Guid(buffer);
        }

        private static void SwapPairs(byte[] buffer, Tuple<int, int>[] pairs)
        {
            if (pairs == null)
                throw new ArgumentNullException(nameof(pairs));

            foreach (var pair in pairs)
            {
                var _ = buffer[pair.Item1];
                buffer[pair.Item1] = buffer[pair.Item2];
                buffer[pair.Item2] = _;
            }
        }
    }
}