using System.Collections;

namespace ReactiveDomain.Util;

public static class EnumerableExtensions {
	public static IEnumerable<T> Safe<T>(this IEnumerable<T>? collection) {
		return collection ?? [];
	}

	public static bool Contains<T>(this IEnumerable<T> collection, Predicate<T> condition) {
		return collection.Any(x => condition(x));
	}

	public static bool IsEmpty<T>(this IEnumerable<T>? collection) =>
		collection switch {
			null => true,
			ICollection coll => coll.Count == 0,
			_ => !collection.Any()
		};

	public static bool IsNotEmpty<T>(this IEnumerable<T> collection) => !collection.IsEmpty();
}
