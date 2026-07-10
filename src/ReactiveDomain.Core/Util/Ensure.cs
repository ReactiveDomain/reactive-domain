
// ReSharper disable AccessToStaticMemberViaDerivedType

namespace ReactiveDomain.Util;

public static class Ensure {
	/// <summary>
	/// Ensures that the argument is not null (throw exception if it is)
	/// </summary>
	/// <typeparam name="T">The type of the argument to test</typeparam>
	/// <param name="argument">The value to test</param>
	/// <param name="argumentName">The name of the argument</param>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="argument"/> is null</exception>
	public static void NotNull<T>(T? argument, string argumentName) where T : class =>
		ArgumentNullException.ThrowIfNull(argument, argumentName);

	/// <summary>
	/// Ensures that the argument (a string) is neither null nor empty (throws an exception if it is)
	/// </summary>
	/// <param name="argument">The value to test</param>
	/// <param name="argumentName">The name of the argument</param>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="argument"/> is null or is an empty string</exception>
	public static void NotNullOrEmpty(string? argument, string argumentName) =>
		ArgumentNullException.ThrowIfNullOrEmpty(argument, argumentName);

	/// <summary>
	/// Ensures that the argument (ICollection) is neither null nor empty (throws an exception if it is)
	/// </summary>
	/// <param name="argument">The value to test</param>
	/// <param name="argumentName">The name of the argument</param>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="argument"/> is null</exception>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="argument"/> contains no elements</exception>
	public static void NotNullOrEmpty<T>(ICollection<T>? argument, string argumentName) {
		ArgumentNullException.ThrowIfNull(argument, argumentName);
		ArgumentOutOfRangeException.ThrowIfZero(argument.Count, argumentName);
	}

	/// <summary>
	/// Ensures that the argument (IEnumerable) is neither null nor empty (throws an exception if it is)
	/// </summary>
	/// <param name="argument">The value to test</param>
	/// <param name="argumentName">The name of the argument</param>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="argument"/> is null</exception>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="argument"/> contains no elements</exception>
	public static void NotNullOrEmpty<T>(IEnumerable<T>? argument, string argumentName) {
		ArgumentNullException.ThrowIfNull(argument, argumentName);
		ArgumentOutOfRangeException.ThrowIfZero(argument.Count(), argumentName);
	}

	/// <summary>
	/// Ensures that the argument (a string) is neither null nor empty (throws an exception if it is)
	/// </summary>
	/// <param name="argument">The value to test</param>
	/// <param name="argumentName">The name of the argument</param>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="argument"/> is null or contains only whitespace characters</exception>
	public static void NotNullOrWhiteSpace(string argument, string argumentName) =>
		ArgumentNullException.ThrowIfNullOrWhiteSpace(argument, argumentName);

	/// <summary>
	/// Ensures that the argument (an int) is &gt;0 (throws an exception if it is &lt;=0)
	/// </summary>
	/// <param name="number">The value to test</param>
	/// <param name="argumentName">The name of the argument</param>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="number"/> is less than or equal to zero</exception>
	public static void Positive(int number, string argumentName) =>
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(number, 0, argumentName);

	/// <summary>
	/// Ensures that the argument (a long) is &gt;0 (throws an exception if it is &lt;=0)
	/// </summary>
	/// <param name="number">The value to test</param>
	/// <param name="argumentName">The name of the argument</param>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="number"/> is less than or equal to zero</exception>
	public static void Positive(long number, string argumentName) =>
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(number, 0, argumentName);

	/// <summary>
	/// Ensures that the argument (a decimal) is &gt;0 (throws an exception if it is &lt;=0)
	/// </summary>
	/// <param name="number">The value to test</param>
	/// <param name="argumentName">The name of the argument</param>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="number"/> is less than or equal to zero</exception>
	public static void Positive(decimal number, string argumentName) =>
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(number, 0, argumentName);

	/// <summary>
	/// Ensures that the argument (a long) is &gt;=0 (throws an exception if it is &lt;0)
	/// </summary>
	/// <param name="number">The value to test</param>
	/// <param name="argumentName">The name of the argument</param>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="number"/> is less than zero</exception>
	public static void Nonnegative(long number, string argumentName) =>
		ArgumentOutOfRangeException.ThrowIfLessThan(number, 0, argumentName);

	/// <summary>
	/// Ensures that the argument (an int) is &gt;=0 (throws an exception if it is &lt;0)
	/// </summary>
	/// <param name="number">The value to test</param>
	/// <param name="argumentName">The name of the argument</param>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="number"/> is less than zero</exception>
	public static void Nonnegative(int number, string argumentName) =>
		ArgumentOutOfRangeException.ThrowIfLessThan(number, 0, argumentName);

	/// <summary>
	/// Ensures that the argument (a decimal) is &gt;=0 (throws an exception if it is &lt;0)
	/// </summary>
	/// <param name="number">The value to test</param>
	/// <param name="argumentName">The name of the argument</param>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="number"/> is less than zero</exception>
	public static void Nonnegative(decimal number, string argumentName) =>
		ArgumentOutOfRangeException.ThrowIfLessThan(number, 0, argumentName);

	/// <summary>
	/// Ensures that the argument (a Guid) is not empty (throws an exception if it is)
	/// </summary>
	/// <param name="guid">The value to test</param>
	/// <param name="argumentName">The name of the argument</param>
	/// <exception cref="ArgumentException">Thrown if <paramref name="guid"/> is an empty Guid</exception>
	public static void NotEmptyGuid(Guid guid, string argumentName) {
		if (Guid.Empty == guid)
			throw new ArgumentException(argumentName + " should be non-empty GUID.", argumentName);
	}

	/// <summary>
	/// Ensures that the argument (an int) is equal to an expected value (throws an exception if it is not)
	/// </summary>
	/// <param name="expected">The expected value</param>
	/// <param name="actual">The value to test</param>
	/// <param name="argumentName">The name of the argument</param>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if the <paramref name="actual"/> and
	/// <paramref name="expected"/> values are not equal</exception>
	public static void Equal(int expected, int actual, string argumentName) =>
		ArgumentOutOfRangeException.ThrowIfNotEqual(actual, expected, argumentName);

	/// <summary>
	/// Ensures that the argument (a long) is equal to an expected value (throws an exception if it is not)
	/// </summary>
	/// <param name="expected">The expected value</param>
	/// <param name="actual">The value to test</param>
	/// <param name="argumentName">The name of the argument</param>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if the <paramref name="actual"/> and
	/// <paramref name="expected"/> values are not equal</exception>
	public static void Equal(long expected, long actual, string argumentName) =>
		ArgumentOutOfRangeException.ThrowIfNotEqual(actual, expected, argumentName);

	/// <summary>
	/// Ensures that the argument (a bool) is equal to an expected value (throws an exception if it is not)
	/// </summary>
	/// <param name="expected">The expected value</param>
	/// <param name="actual">The value to test</param>
	/// <param name="argumentName">The name of the argument</param>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if the <paramref name="actual"/> and
	/// <paramref name="expected"/> values are not equal</exception>
	public static void Equal(bool expected, bool actual, string argumentName) =>
		ArgumentOutOfRangeException.ThrowIfNotEqual(actual, expected, argumentName);

	/// <summary>
	/// Ensures that the argument (a Guid) is equal to an expected value (throws an exception if it is not)
	/// </summary>
	/// <param name="expected">The expected value</param>
	/// <param name="actual">The value to test</param>
	/// <param name="argumentName">The name of the argument</param>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if the <paramref name="actual"/> and
	/// <paramref name="expected"/> values are not equal</exception>
	public static void Equal(Guid expected, Guid actual, string argumentName) =>
		ArgumentOutOfRangeException.ThrowIfNotEqual(actual, expected, argumentName);

	/// <summary>
	/// Ensures that the argument (an int) is not equal to an expected value (throws an exception if they are equal)
	/// </summary>
	/// <param name="expected">The expected value</param>
	/// <param name="actual">The value to test</param>
	/// <param name="argumentName">The name of the argument</param>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if the <paramref name="actual"/> and
	/// <paramref name="expected"/> values are equal</exception>
	public static void NotEqual(int expected, int actual, string argumentName) =>
		ArgumentOutOfRangeException.ThrowIfEqual(actual, expected, argumentName);

	/// <summary>
	/// Ensures that the argument (a long) is not equal to an expected value (throws an exception if they are equal)
	/// </summary>
	/// <param name="expected">The expected value</param>
	/// <param name="actual">The value to test</param>
	/// <param name="argumentName">The name of the argument</param>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if the <paramref name="actual"/> and
	/// <paramref name="expected"/> values are equal</exception>
	public static void NotEqual(long expected, long actual, string argumentName) =>
		ArgumentOutOfRangeException.ThrowIfEqual(actual, expected, argumentName);

	/// <summary>
	/// Ensures that the argument (a bool) is not equal to an expected value (throws an exception if they are equal)
	/// </summary>
	/// <param name="expected">The expected value</param>
	/// <param name="actual">The value to test</param>
	/// <param name="argumentName">The name of the argument</param>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if the <paramref name="actual"/> and
	/// <paramref name="expected"/> values are equal</exception>
	public static void NotEqual(bool expected, bool actual, string argumentName) =>
		ArgumentOutOfRangeException.ThrowIfEqual(actual, expected, argumentName);

	/// <summary>
	/// Ensures that the argument (a Guid) is not equal to an expected value (throws an exception if they are equal)
	/// </summary>
	/// <param name="expected">The expected value</param>
	/// <param name="actual">The value to test</param>
	/// <param name="argumentName">The name of the argument</param>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if the <paramref name="actual"/> and
	/// <paramref name="expected"/> values are equal</exception>
	public static void NotEqual(Guid expected, Guid actual, string argumentName) =>
		ArgumentOutOfRangeException.ThrowIfEqual(actual, expected, argumentName);

	/// <summary>
	/// Ensures that the argument (an int) is equal to SOME (any) power of 2 (throws an exception if it is not)
	/// </summary>
	/// <param name="argument">The value to test</param>
	/// <param name="argumentName">The name of the argument</param>
	/// <exception cref="ArgumentException">Thrown if <paramref name="argument"/> is not a power of two</exception>
	public static void PowerOf2(int argument, string argumentName) {
		if (argument <= 0 || ((uint)argument & ((uint)argument - 1)) != 0)
			throw new ArgumentException($"{argument} is not a power of 2", argumentName);
	}

	/// <summary>
	/// Ensures that the argument (an int) is &lt; some expected value (throws an exception if it is &gt;= expected)
	/// </summary>
	/// <param name="expected">The <paramref name="actual"/> value is expected to be less than this</param>
	/// <param name="actual">The value to test</param>
	/// <param name="argumentName">The name of the argument</param>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="actual"/> is not less than <paramref name="expected"/></exception>
	public static void LessThan(int expected, int actual, string argumentName) =>
		ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(actual, expected, argumentName);

	/// <summary>
	/// Ensures that the argument (a long) is &lt; some expected value (throws an exception if it is &gt;= expected)
	/// </summary>
	/// <param name="expected">The <paramref name="actual"/> value is expected to be less than this</param>
	/// <param name="actual">The value to test</param>
	/// <param name="argumentName">The name of the argument</param>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="actual"/> is not less than <paramref name="expected"/></exception>
	public static void LessThan(long expected, long actual, string argumentName) =>
		ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(actual, expected, argumentName);

	/// <summary>
	/// Ensures that the argument (a decimal) is &lt; some expected value (throws an exception if it is &gt;= expected)
	/// </summary>
	/// <param name="expected">The <paramref name="actual"/> value is expected to be less than this</param>
	/// <param name="actual">The value to test</param>
	/// <param name="argumentName">The name of the argument</param>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="actual"/> is not less than <paramref name="expected"/></exception>
	public static void LessThan(decimal expected, decimal actual, string argumentName) {
		ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(actual, expected, argumentName);
	}

	/// <summary>
	/// Ensures that the argument (an int) is &lt;= some expected value (throws an exception if it is &gt; expected)
	/// </summary>
	/// <param name="expected">The <paramref name="actual"/> value is expected to be less than or equal to this</param>
	/// <param name="actual">The value to test</param>
	/// <param name="argumentName">The name of the argument</param>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="actual"/> is not less than or equal to <paramref name="expected"/></exception>
	public static void LessThanOrEqualTo(int expected, int actual, string argumentName) =>
		ArgumentOutOfRangeException.ThrowIfGreaterThan(actual, expected, argumentName);

	/// <summary>
	/// Ensures that the argument (a long) is &lt;= some expected value (throws an exception if it is &gt; expected)
	/// </summary>
	/// <param name="expected">The <paramref name="actual"/> value is expected to be less than or equal to this</param>
	/// <param name="actual">The value to test</param>
	/// <param name="argumentName">The name of the argument</param>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="actual"/> is not less than or equal to <paramref name="expected"/></exception>
	public static void LessThanOrEqualTo(long expected, long actual, string argumentName) =>
		ArgumentOutOfRangeException.ThrowIfGreaterThan(actual, expected, argumentName);

	/// <summary>
	/// Ensures that the argument (a decimal) is &lt;= some expected value (throws an exception if it is &gt; expected)
	/// </summary>
	/// <param name="expected">The <paramref name="actual"/> value is expected to be less than or equal to this</param>
	/// <param name="actual">The value to test</param>
	/// <param name="argumentName">The name of the argument</param>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="actual"/> is not less than or equal to <paramref name="expected"/></exception>
	public static void LessThanOrEqualTo(decimal expected, decimal actual, string argumentName) =>
		ArgumentOutOfRangeException.ThrowIfGreaterThan(actual, expected, argumentName);

	/// <summary>
	/// Ensures that the argument (an int) is &gt; some expected value (throws an exception if it is &lt;= expected)
	/// </summary>
	/// <param name="expected">The <paramref name="actual"/> value is expected to be greater than this</param>
	/// <param name="actual">The value to test</param>
	/// <param name="argumentName">The name of the argument</param>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="actual"/> is not greater than <paramref name="expected"/></exception>
	public static void GreaterThan(int expected, int actual, string argumentName) =>
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(actual, expected, argumentName);

	/// <summary>
	/// Ensures that the argument (a long) is &gt; some expected value (throws an exception if it is &lt;= expected)
	/// </summary>
	/// <param name="expected">The <paramref name="actual"/> value is expected to be greater than this</param>
	/// <param name="actual">The value to test</param>
	/// <param name="argumentName">The name of the argument</param>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="actual"/> is not greater than <paramref name="expected"/></exception>
	public static void GreaterThan(long expected, long actual, string argumentName) =>
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(actual, expected, argumentName);

	/// <summary>
	/// Ensures that the argument (a decimal) is &gt; some expected value (throws an exception if it is &lt;= expected)
	/// </summary>
	/// <param name="expected">The <paramref name="actual"/> value is expected to be greater than this</param>
	/// <param name="actual">The value to test</param>
	/// <param name="argumentName">The name of the argument</param>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="actual"/> is not greater than <paramref name="expected"/></exception>
	public static void GreaterThan(decimal expected, decimal actual, string argumentName) =>
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(actual, expected, argumentName);

	/// <summary>
	/// Ensures that the argument (an int) is &gt;= some expected value (throws an exception if it is &lt; expected)
	/// </summary>
	/// <param name="expected">The <paramref name="actual"/> value is expected to be greater than or equal to this</param>
	/// <param name="actual">The value to test</param>
	/// <param name="argumentName">The name of the argument</param>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="actual"/> is not greater than or equal to <paramref name="expected"/></exception>
	public static void GreaterThanOrEqualTo(int expected, int actual, string argumentName) =>
		ArgumentOutOfRangeException.ThrowIfLessThan(actual, expected, argumentName);

	/// <summary>
	/// Ensures that the argument (a long) is &gt;= some expected value (throws an exception if it is &lt; expected)
	/// </summary>
	/// <param name="expected">The <paramref name="actual"/> value is expected to be greater than or equal to this</param>
	/// <param name="actual">The value to test</param>
	/// <param name="argumentName">The name of the argument</param>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="actual"/> is not greater than or equal to <paramref name="expected"/></exception>
	public static void GreaterThanOrEqualTo(long expected, long actual, string argumentName) =>
		ArgumentOutOfRangeException.ThrowIfLessThan(actual, expected, argumentName);

	/// <summary>
	/// Ensures that the argument (a decimal) is &gt;= some expected value (throws an exception if it is &lt; expected)
	/// </summary>
	/// <param name="expected">The <paramref name="actual"/> value is expected to be greater than or equal to this</param>
	/// <param name="actual">The value to test</param>
	/// <param name="argumentName">The name of the argument</param>
	public static void GreaterThanOrEqualTo(decimal expected, decimal actual, string argumentName) =>
		ArgumentOutOfRangeException.ThrowIfLessThan(actual, expected, argumentName);

	/// <summary>
	/// Ensures that the argument (an int) is between 2 other values such that low &lt; argument &gt; high (throws an exception if it is not)
	/// </summary>
	/// <param name="low">The <paramref name="actual"/> value is expected to be greater than this value</param>
	/// <param name="high">The <paramref name="actual"/> value is expected to be less than this value</param>
	/// <param name="actual">The value to test</param>
	/// <param name="argumentName">The name of the argument</param>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="actual"/> is not between <paramref name="low"/> and <paramref name="high"/></exception>
	public static void Between(int low, int high, int actual, string argumentName) {
		if (actual <= low || actual >= high)
			throw new ArgumentOutOfRangeException(
				$"{argumentName} expected to be between {low} and {high}, actual value: {actual}");
	}

	/// <summary>
	/// Ensures that the argument (a long) is between 2 other values such that low &lt; argument &gt; high (throws an exception if it is not)
	/// </summary>
	/// <param name="low">The <paramref name="actual"/> value is expected to be greater than this value</param>
	/// <param name="high">The <paramref name="actual"/> value is expected to be less than this value</param>
	/// <param name="actual">The value to test</param>
	/// <param name="argumentName">The name of the argument</param>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="actual"/> is not between <paramref name="low"/> and <paramref name="high"/></exception>
	public static void Between(long low, long high, long actual, string argumentName) {
		if (actual <= low || actual >= high)
			throw new ArgumentOutOfRangeException(
				$"{argumentName} expected to be between {low} and {high}, actual value: {actual}");
	}

	/// <summary>
	/// Ensures that the argument (a decimal) is between 2 other values such that low &lt; argument &gt; high (throws an exception if it is not)
	/// </summary>
	/// <param name="low">The <paramref name="actual"/> value is expected to be greater than this value</param>
	/// <param name="high">The <paramref name="actual"/> value is expected to be less than this value</param>
	/// <param name="actual">The value to test</param>
	/// <param name="argumentName">The name of the argument</param>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="actual"/> is not between <paramref name="low"/> and <paramref name="high"/></exception>
	public static void Between(decimal low, decimal high, decimal actual, string argumentName) {
		if (actual <= low || actual >= high)
			throw new ArgumentOutOfRangeException(
				$"{argumentName} expected to be between {low} and {high}, actual value: {actual}");
	}

	/// <summary>
	/// Ensures that the argument (an int) is between or equal to 2 other values such that low &lt;= argument &gt;= high (throws an exception if it is not)
	/// </summary>
	/// <param name="low">The <paramref name="actual"/> value is expected to be greater than or equal to this value</param>
	/// <param name="high">The <paramref name="actual"/> value is expected to be less than or equal to this value</param>
	/// <param name="actual">The value to test</param>
	/// <param name="argumentName">The name of the argument</param>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="actual"/> is not in the range between
	/// and including <paramref name="low"/> and <paramref name="high"/></exception>
	public static void BetweenOrEqual(int low, int high, int actual, string argumentName) {
		if (actual < low || actual > high)
			throw new ArgumentOutOfRangeException(
				$"{argumentName} expected to be between {low} and {high} or equal, actual value: {actual}");
	}

	/// <summary>
	/// Ensures that the argument (a long) is between or equal to 2 other values such that low &lt;= argument &gt;= high (throws an exception if it is not)
	/// </summary>
	/// <param name="low">The <paramref name="actual"/> value is expected to be greater than or equal to this value</param>
	/// <param name="high">The <paramref name="actual"/> value is expected to be less than or equal to this value</param>
	/// <param name="actual">The value to test</param>
	/// <param name="argumentName">The name of the argument</param>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="actual"/> is not in the range between
	/// and including <paramref name="low"/> and <paramref name="high"/></exception>
	public static void BetweenOrEqual(long low, long high, long actual, string argumentName) {
		if (actual < low || actual > high)
			throw new ArgumentOutOfRangeException(
				$"{argumentName} expected to be between {low} and {high} or equal, actual value: {actual}");
	}

	/// <summary>
	/// Ensures that the argument (a decimal) is between or equal to 2 other values such that low &lt;= argument &gt;= high (throws an exception if it is not)
	/// </summary>
	/// <param name="low">The <paramref name="actual"/> value is expected to be greater than or equal to this value</param>
	/// <param name="high">The <paramref name="actual"/> value is expected to be less than or equal to this value</param>
	/// <param name="actual">The value to test</param>
	/// <param name="argumentName">The name of the argument</param>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="actual"/> is not in the range between
	/// and including <paramref name="low"/> and <paramref name="high"/></exception>
	public static void BetweenOrEqual(decimal low, decimal high, decimal actual, string argumentName) {
		if (actual < low || actual > high)
			throw new ArgumentOutOfRangeException(
				$"{argumentName} expected to be between {low} and {high} or equal, actual value: {actual}");
	}

	/// <summary>
	/// Ensures that expected is not equal to the uninitialized value, generally indicating that it has been set (throws an exception if it is)
	/// </summary>
	/// <param name="argument">The value to test</param>
	/// <param name="argumentName">The name of the argument</param>
	/// <exception cref="ArgumentException">Thrown if <paramref name="argument"/> is equal to the default value for <see cref="DateTime"/></exception>
	public static void NotDefault(DateTime argument, string argumentName) {
		if (argument == default)
			throw new ArgumentException($"{argumentName} is equal to the default value :{DateTime.MinValue}",
				argumentName);
	}

	/// <summary>
	/// Ensures that the dictionary contains the specified key (throws an exception if it does not)
	/// </summary>
	/// <param name="lookup">The dictionary to search in</param>
	/// <param name="key">The key to search for</param>
	/// <param name="argumentName">The name of the argument</param>
	/// <exception cref="ArgumentException">Thrown if <paramref name="lookup"/> does not contain the <paramref name="key"/></exception>
	public static void ContainsKey<TKey, TValue>(Dictionary<TKey, TValue> lookup, TKey key, string argumentName)
		where TKey : notnull {
		NotNull(lookup, argumentName);
		if (!lookup.ContainsKey(key))
			throw new ArgumentException($"{argumentName} expected to contain the key {key}, but it was not found.",
				argumentName);
	}

	/// <summary>
	/// Ensures that the dictionary does not contain the specified key (throws an exception if it does)
	/// </summary>
	/// <param name="lookup">The dictionary to search in</param>
	/// <param name="key">The key to search for</param>
	/// <param name="argumentName">The name of the argument</param>
	/// <exception cref="ArgumentException">Thrown if <paramref name="lookup"/> contains the <paramref name="key"/></exception>
	public static void DoesNotContainKey<TKey, TValue>(Dictionary<TKey, TValue> lookup, TKey key, string argumentName)
		where TKey : notnull {
		NotNull(lookup, argumentName);
		if (lookup.ContainsKey(key))
			throw new ArgumentException($"{argumentName} contain the key {key}.", argumentName);
	}

	/// <summary>
	/// Ensures that the enumerable contains the specified item (throws an exception if it is not)
	/// </summary>
	/// <param name="lookup">The enumerable to search in</param>
	/// <param name="value">The value to search for</param>
	/// <param name="argumentName">The name of the argument</param>
	/// <exception cref="ArgumentException">Thrown if <paramref name="lookup"/> does not contain the <paramref name="value"/></exception>
	public static void Contains<T>(IEnumerable<T> lookup, T value, string argumentName) {
		if (!lookup.Contains(value))
			throw new ArgumentException($"{argumentName} expected to contain the value {value}, but it was not found.",
				argumentName);
	}

	/// <summary>
	/// Ensures that the enumerable does not contain the specified item (throws an exception if it is not)
	/// </summary>
	/// <param name="lookup">The enumerable to search in</param>
	/// <param name="value">The value to search for</param>
	/// <param name="argumentName">The name of the argument</param>
	/// <exception cref="ArgumentException">Thrown if <paramref name="lookup"/> contains the <paramref name="value"/></exception>
	public static void DoesNotContain<T>(IEnumerable<T> lookup, T value, string argumentName) {
		if (lookup.Contains(value))
			throw new ArgumentException($"{argumentName} contains the value {value}.", argumentName);
	}

	/// <summary>
	/// Ensures that the expression is true (throws an exception if it is false)
	/// </summary>
	/// <param name="expression">The expression to test</param>
	/// <param name="expressionAsString">A string representation of the expression, to be included in the exception message.</param>
	/// <exception cref="ArgumentException">Thrown if the expression is false when evaluated</exception>
	public static void True(Func<bool> expression, string expressionAsString) {
		if (!expression())
			throw new ArgumentException($"{expressionAsString} expected to be true, but is found to be false.");
	}

	/// <summary>
	/// Ensures that the expression is false (throws an exception if it is true)
	/// </summary>
	/// <param name="expression">The expression to test</param>
	/// <param name="expressionAsString">A string representation of the expression, to be included in the exception message.</param>
	/// <exception cref="ArgumentException">Thrown if the expression is true when evaluated</exception>
	public static void False(Func<bool> expression, string expressionAsString) {
		if (expression())
			throw new ArgumentException($"{expressionAsString} expected to be false, but is found to be true.");
	}
}
