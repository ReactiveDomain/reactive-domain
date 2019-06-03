using System;
using System.Collections.Generic;
using System.Linq;

namespace ReactiveDomain.Util
{
    public static class Ensure
    {
        /// <summary>
        /// Ensure that the argument is not null (throw exception if it is)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="argument"></param>
        /// <param name="argumentName"></param>
        public static void NotNull<T>(T argument, string argumentName) where T : class
        {
            if (argument == null)
                throw new ArgumentNullException(argumentName);
        }

        /// <summary>
        /// Ensure that the argument (a string) is neither null nor empty (throw an exception if it is)
        /// </summary>
        /// <param name="argument"></param>
        /// <param name="argumentName"></param>
        public static void NotNullOrEmpty(string argument, string argumentName)
        {
            if (string.IsNullOrEmpty(argument))
                throw new ArgumentNullException(argument, argumentName);
        }
        /// <summary>
        /// Ensure that the argument (ICollection) is neither null nor empty (throw an exception if it is)
        /// </summary>
        /// <param name="argument"></param>
        /// <param name="argumentName"></param>
        public static void NotNullOrEmpty<T>(ICollection<T> argument, string argumentName)
        {
            if (argument == null)
                throw new ArgumentNullException(argumentName);
            if (argument.Count < 1)
                throw new ArgumentOutOfRangeException(argumentName, argumentName + " must have items.");
        }

        /// <summary>
        /// Ensure that the argument (an int) is &gt;0 (throw an exception if it is &lt;=0)
        /// </summary>
        /// <param name="number"></param>
        /// <param name="argumentName"></param>
        public static void Positive(int number, string argumentName)
        {
            if (number <= 0)
                throw new ArgumentOutOfRangeException(argumentName, argumentName + " should be positive.");
        }

        /// <summary>
        /// Ensure that the argument (a long) is &gt;0 (throw an exception if it is &lt;=0)
        /// </summary>
        /// <param name="number"></param>
        /// <param name="argumentName"></param>
        public static void Positive(long number, string argumentName)
        {
            if (number <= 0)
                throw new ArgumentOutOfRangeException(argumentName, argumentName + " should be positive.");
        }

        /// <summary>
        /// Ensure that the argument (a decimal) is &gt;0 (throw an exception if it is &lt;=0)
        /// </summary>
        /// <param name="number"></param>
        /// <param name="argumentName"></param>
        public static void Positive(decimal number, string argumentName)
        {
            if (number <= 0)
                throw new ArgumentOutOfRangeException(argumentName, argumentName + " should be positive.");
        }

        /// <summary>
        /// Ensure that the argument (a long) is &gt;=0 (throw an exception if it is &lt;0)
        /// </summary>
        /// <param name="number"></param>
        /// <param name="argumentName"></param>
        public static void Nonnegative(long number, string argumentName)
        {
            if (number < 0)
                throw new ArgumentOutOfRangeException(argumentName, argumentName + " should be non negative.");
        }

        /// <summary>
        /// Ensure that the argument (an int) is &gt;=0 (throw an exception if it is &lt;0)
        /// </summary>
        /// <param name="number"></param>
        /// <param name="argumentName"></param>
        public static void Nonnegative(int number, string argumentName)
        {
            if (number < 0)
                throw new ArgumentOutOfRangeException(argumentName, argumentName + " should be non negative.");
        }

        /// <summary>
        /// Ensure that the argument (a decimal) is &gt;=0 (throw an exception if it is &lt;0)
        /// </summary>
        /// <param name="number"></param>
        /// <param name="argumentName"></param>
        public static void Nonnegative(decimal number, string argumentName)
        {
            if (number < 0)
                throw new ArgumentOutOfRangeException(argumentName, argumentName + " should be non negative.");
        }

        /// <summary>
        /// Ensure that the argument (a Guid) is not empty (throw an exception if it is)
        /// </summary>
        /// <param name="guid"></param>
        /// <param name="argumentName"></param>
        public static void NotEmptyGuid(Guid guid, string argumentName)
        {
            if (Guid.Empty == guid)
                throw new ArgumentException(argumentName, argumentName + " shoud be non-empty GUID.");
        }

        /// <summary>
        /// Ensure that the argument (an int) is equal to an expected value (throw an exception if it is not)
        /// </summary>
        /// <param name="expected"></param>
        /// <param name="actual"></param>
        /// <param name="argumentName"></param>
        public static void Equal(int expected, int actual, string argumentName)
        {
            if (expected != actual)
                throw new ArgumentException($"{argumentName} expected value: {expected}, actual value: {actual}");
        }

        /// <summary>
        /// Ensure that the argument (a long) is equal to an expected value (throw an exception if it is not)
        /// </summary>
        /// <param name="expected"></param>
        /// <param name="actual"></param>
        /// <param name="argumentName"></param>
        public static void Equal(long expected, long actual, string argumentName)
        {
            if (expected != actual)
                throw new ArgumentException($"{argumentName} expected value: {expected}, actual value: {actual}");
        }

        /// <summary>
        /// Ensure that the argument (a bool) is equal to an expected value (throw an exception if it is not)
        /// </summary>
        /// <param name="expected"></param>
        /// <param name="actual"></param>
        /// <param name="argumentName"></param>
        public static void Equal(bool expected, bool actual, string argumentName)
        {
            if (expected != actual)
                throw new ArgumentException($"{argumentName} expected value: {expected}, actual value: {actual}");
        }

        /// <summary>
        /// Ensure that the argument (a Guid) is equal to an expected value (throw an exception if it is not)
        /// </summary>
        /// <param name="expected"></param>
        /// <param name="actual"></param>
        /// <param name="argumentName"></param>
        public static void Equal(Guid expected, Guid actual, string argumentName)
        {
            if (expected != actual)
                throw new ArgumentException($"{argumentName} expected value: {expected}, actual value: {actual}");
        }

        /// <summary>
        /// Ensure that the argument (an int) is equal to SOME (any) power of 2 (throw an exception if it is not)
        /// </summary>
        /// <param name="argument"></param>
        /// <param name="argumentName"></param>
        public static void PowerOf2(int argument, string argumentName)
        {
            if ((argument <= 0) || (((uint)argument) & ((uint)argument - 1)) != 0)
                throw new ArgumentException($"{argumentName}: {argument} is not a power of 2");
        }

        /// <summary>
        /// Ensure that the argument (an int) is &lt; some expected value (throw an exception if it is &gt;= expected)
        /// </summary>
        /// <param name="expected"></param>
        /// <param name="argument"></param>
        /// <param name="argumentName"></param>
        public static void LessThan(int expected, int argument, string argumentName)
        {
            if (argument >= expected)
                throw new ArgumentException(
                    $"{argumentName} expected to be less than: {expected}, actual value: {argument}");
        }

        /// <summary>
        /// Ensure that the argument (a long) is &lt; some expected value (throw an exception if it is &gt;= expected)
        /// </summary>
        /// <param name="expected"></param>
        /// <param name="argument"></param>
        /// <param name="argumentName"></param>
        public static void LessThan(long expected, long argument, string argumentName)
        {
            if (argument >= expected)
                throw new ArgumentException(
                    $"{argumentName} expected to be less than: {expected}, actual value: {argument}");
        }

        /// <summary>
        /// Ensure that the argument (a decimal) is &lt; some expected value (throw an exception if it is &gt;= expected)
        /// </summary>
        /// <param name="expected"></param>
        /// <param name="argument"></param>
        /// <param name="argumentName"></param>
        public static void LessThan(decimal expected, decimal argument, string argumentName)
        {
            if (argument >= expected)
                throw new ArgumentException(
                    $"{argumentName} expected to be less than: {expected}, actual value: {argument}");
        }

        /// <summary>
        /// Ensure that the argument (an int) is &lt;= some expected value (throw an exception if it is &gt; expected)
        /// </summary>
        /// <param name="expected"></param>
        /// <param name="argument"></param>
        /// <param name="argumentName"></param>
        public static void LessThanOrEqualTo(int expected, int argument, string argumentName)
        {
            if (argument > expected)
                throw new ArgumentException(
                    $"{argumentName} expected to be less than: {expected}, actual value: {argument}");
        }

        /// <summary>
        /// Ensure that the argument (a long) is &lt;= some expected value (throw an exception if it is &gt; expected)
        /// </summary>
        /// <param name="expected"></param>
        /// <param name="argument"></param>
        /// <param name="argumentName"></param>
        public static void LessThanOrEqualTo(long expected, long argument, string argumentName)
        {
            if (argument > expected)
                throw new ArgumentException(
                    $"{argumentName} expected to be less than: {expected}, actual value: {argument}");
        }

        /// <summary>
        /// Ensure that the argument (a decimal) is &lt;= some expected value (throw an exception if it is &gt; expected)
        /// </summary>
        /// <param name="expected"></param>
        /// <param name="argument"></param>
        /// <param name="argumentName"></param>
        public static void LessThanOrEqualTo(decimal expected, decimal argument, string argumentName)
        {
            if (argument > expected)
                throw new ArgumentException(
                    $"{argumentName} expected to be less than: {expected}, actual value: {argument}");
        }
        /// <summary>
        /// Ensure that the argument (an int) is &gt; some expected value (throw an exception if it is &lt;= expected)
        /// </summary>
        /// <param name="expected"></param>
        /// <param name="argument"></param>
        /// <param name="argumentName"></param>
        public static void GreaterThan(int expected, int argument, string argumentName)
        {
            if (argument <= expected)
                throw new ArgumentException(
                    $"{argumentName} expected to be greater than: {expected}, actual value: {argument}");
        }

        /// <summary>
        /// Ensure that the argument (a long) is &gt; some expected value (throw an exception if it is &lt;= expected)
        /// </summary>
        /// <param name="expected"></param>
        /// <param name="argument"></param>
        /// <param name="argumentName"></param>
        public static void GreaterThan(long expected, long argument, string argumentName)
        {
            if (argument <= expected)
                throw new ArgumentException(
                    $"{argumentName} expected to be greater than: {expected}, actual value: {argument}");
        }

        /// <summary>
        /// Ensure that the argument (a decimal) is &gt; some expected value (throw an exception if it is &lt;= expected)
        /// </summary>
        /// <param name="expected"></param>
        /// <param name="argument"></param>
        /// <param name="argumentName"></param>
        public static void GreaterThan(decimal expected, decimal argument, string argumentName)
        {
            if (argument <= expected)
                throw new ArgumentException(
                    $"{argumentName} expected to be greater than: {expected}, actual value: {argument}");
        }

        /// <summary>
        /// Ensure that the argument (an int) is &gt;= some expected value (throw an exception if it is &lt; expected)
        /// </summary>
        /// <param name="expected"></param>
        /// <param name="argument"></param>
        /// <param name="argumentName"></param>
        public static void GreaterThanOrEqualTo(int expected, int argument, string argumentName)
        {
            if (argument < expected)
                throw new ArgumentException(
                    $"{argumentName} expected to be greater than: {expected}, actual value: {argument}");
        }

        /// <summary>
        /// Ensure that the argument (a long) is &gt;= some expected value (throw an exception if it is &lt; expected)
        /// </summary>
        /// <param name="expected"></param>
        /// <param name="argument"></param>
        /// <param name="argumentName"></param>
        public static void GreaterThanOrEqualTo(long expected, long argument, string argumentName)
        {
            if (argument < expected)
                throw new ArgumentException(
                    $"{argumentName} expected to be greater than: {expected}, actual value: {argument}");
        }

        /// <summary>
        /// Ensure that the argument (a decimal) is &gt;= some expected value (throw an exception if it is &lt; expected)
        /// </summary>
        /// <param name="expected"></param>
        /// <param name="argument"></param>
        /// <param name="argumentName"></param>
        public static void GreaterThanOrEqualTo(decimal expected, decimal argument, string argumentName)
        {
            if (argument < expected)
                throw new ArgumentException(
                    $"{argumentName} expected to be greater than: {expected}, actual value: {argument}");
        }

        /// <summary>
        /// Ensure that the argument (an int) is between 2 other values such that low &lt; argument &gt; high (throw an exception if it is not)
        /// </summary>
        /// <param name="low"></param>
        /// <param name="high"></param>
        /// <param name="argument"></param>
        /// <param name="argumentName"></param>
        public static void Between(int low, int high, int argument, string argumentName)
        {
            if (argument <= low || argument >= high)

                throw new ArgumentException(
                    $"{argumentName} expected to be between {low} and {high}, actual value: {argument}");
        }

        /// <summary>
        /// Ensure that the argument (a long) is between 2 other values such that low &lt; argument &gt; high (throw an exception if it is not)
        /// </summary>
        /// <param name="low"></param>
        /// <param name="high"></param>
        /// <param name="argument"></param>
        /// <param name="argumentName"></param>
        public static void Between(long low, long high, long argument, string argumentName)
        {
            if (argument <= low || argument >= high)
                throw new ArgumentException(
                    $"{argumentName} expected to be between {low} and {high}, actual value: {argument}");
        }

        /// <summary>
        /// Ensure that the argument (a decimal) is between 2 other values such that low &lt; argument &gt; high (throw an exception if it is not)
        /// </summary>
        /// <param name="low"></param>
        /// <param name="high"></param>
        /// <param name="argument"></param>
        /// <param name="argumentName"></param>
        public static void Between(decimal low, decimal high, decimal argument, string argumentName)
        {
            if (argument <= low || argument >= high)
                throw new ArgumentException(
                    $"{argumentName} expected to be between {low} and {high}, actual value: {argument}");
        }

        /// <summary>
        /// Ensure that the argument (an int) is between or equal to 2 other values such that low &lt;= argument &gt;= high (throw an exception if it is not)
        /// </summary>
        /// <param name="low"></param>
        /// <param name="high"></param>
        /// <param name="argument"></param>
        /// <param name="argumentName"></param>
        public static void BetweenOrEqual(int low, int high, int argument, string argumentName)
        {
            if (argument < low || argument > high)
                throw new ArgumentException(
                    $"{argumentName} expected to be between {low} and {high} or equal, actual value: {argument}");
        }

        /// <summary>
        /// Ensure that the argument (a long) is between or equal to 2 other values such that low &lt;= argument &gt;= high (throw an exception if it is not)
        /// </summary>
        /// <param name="low"></param>
        /// <param name="high"></param>
        /// <param name="argument"></param>
        /// <param name="argumentName"></param>
        public static void BetweenOrEqual(long low, long high, long argument, string argumentName)
        {
            if (argument < low || argument > high)
                throw new ArgumentException(
                    $"{argumentName} expected to be between {low} and {high} or equal, actual value: {argument}");
        }

        /// <summary>
        /// Ensure that the argument (a decimal) is between or equal to 2 other values such that low &lt;= argument &gt;= high (throw an exception if it is not)
        /// </summary>
        /// <param name="low"></param>
        /// <param name="high"></param>
        /// <param name="argument"></param>
        /// <param name="argumentName"></param>
        public static void BetweenOrEqual(decimal low, decimal high, decimal argument, string argumentName)
        {
            if (argument < low || argument > high)
                throw new ArgumentException(
                    $"{argumentName} expected to be between {low} and {high} or equal, actual value: {argument}");
        }
        /// <summary>
        /// Ensure that expected is not equal to the uninitialized value, generally indicating that is has been set (throw an exception if it is)
        /// </summary>
        /// <param name="expected"></param>
        /// <param name="argumentName"></param>
        public static void NotDefault(DateTime expected, string argumentName)
        {
            if (expected == DateTime.MinValue)
                throw new ArgumentException(argumentName, $"{argumentName} is equal to the default value :{DateTime.MinValue}");
        }

        /// <summary>
        /// Ensure that the dictionary contains the specified key (throw an exception if it is not)
        /// </summary>
        /// <param name="lookup"></param>
        /// <param name="key"></param>
        /// <param name="argumentName"></param>
        public static void ContainsKey<TKey, TValue>(Dictionary<TKey, TValue> lookup, TKey key, string argumentName)
        {
            NotNull(lookup, argumentName);
            if (!lookup.ContainsKey(key))
                throw new ArgumentException(
                    $"{argumentName} expected to contain the key {key}, but it was not found.");
        }

        /// <summary>
        /// Ensure that the enumerable contains the specified item (throw an exception if it is not)
        /// </summary>
        /// <param name="lookup"></param>
        /// <param name="value"></param>
        /// <param name="argumentName"></param>
        public static void Contains<T>(IEnumerable<T> lookup, T value, string argumentName)
        {
            // ReSharper disable PossibleMultipleEnumeration
            NotNull(lookup, argumentName);
            if (!lookup.Contains(value))
                throw new ArgumentException(
                    $"{argumentName} expected to contain the value {value}, but it was not found.");
            // ReSharper restore PossibleMultipleEnumeration
        }

        public static void True(Func<bool> expression, string expressionAsString)
        {
            if (!expression())
                throw new ArgumentException($"{expressionAsString} expected to be true, but is found to be false.");
        }

        public static void False(Func<bool> expression, string expressionAsString)
        {
            if (expression())
                throw new ArgumentException($"{expressionAsString} expected to be false, but is found to be true.");
        }
    }
}