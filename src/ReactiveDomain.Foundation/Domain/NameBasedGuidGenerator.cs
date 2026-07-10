using System.Security.Cryptography;

// ReSharper disable once CheckNamespace
namespace ReactiveDomain;

public class NameBasedGuidGenerator {
	private static readonly Tuple<int, int>[] _byteOrderPairsToSwap = [
		Tuple.Create(0, 3),
		Tuple.Create(1, 2),
		Tuple.Create(4, 5),
		Tuple.Create(6, 7)
	];

	private readonly byte[] _namespace;

	public NameBasedGuidGenerator(Guid @namespace) {
		_namespace = @namespace.ToByteArray();
		SwapPairs(_namespace, _byteOrderPairsToSwap);
	}

	public Guid Create(byte[] input) {
		byte[] hash;
		using (var algorithm = SHA1.Create()) {
			algorithm.TransformBlock(_namespace, 0, _namespace.Length, null, 0);
			algorithm.TransformFinalBlock(input, 0, input.Length);
			hash = algorithm.Hash!;
		}

		var buffer = new byte[16];
		Array.Copy(hash, 0, buffer, 0, 16);

		buffer[6] = (byte)((buffer[6] & 0x0F) | (5 << 4));
		buffer[8] = (byte)((buffer[8] & 0x3F) | 0x80);

		SwapPairs(buffer, _byteOrderPairsToSwap);
		return new Guid(buffer);
	}

	private static void SwapPairs(byte[] buffer, Tuple<int, int>[] pairs) {
		ArgumentNullException.ThrowIfNull(pairs);

		foreach (var pair in pairs) {
			(buffer[pair.Item1], buffer[pair.Item2]) = (buffer[pair.Item2], buffer[pair.Item1]);
		}
	}
}
