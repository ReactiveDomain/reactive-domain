using ReactiveDomain.Util;
using Xunit;

namespace ReactiveDomain.Testing.Tests;

/// <summary>
/// Tests of <see cref="ReactiveDomain.Util.Ensure"/> since the Core project doesn't have its own Tests project.
/// </summary>
public class EnsureTests {
	[Fact]
	public void NotNullThrowsOnNull() {
		string? arg = null;
		Assert.Throws<ArgumentNullException>(() => Ensure.NotNull(arg, nameof(arg)));
	}

	[Fact]
	public void NotNullDoesNotThrowWhenNotNull() {
		const string? arg = "not null";
		Ensure.NotNull(arg, nameof(arg));
	}

	[Theory]
	[InlineData(null, typeof(ArgumentNullException))]
	[InlineData("", typeof(ArgumentException))]
	public void NotNullOrEmptyThrowsOnStringAsExpected(string? arg, Type exceptionType) {
		Assert.Throws(exceptionType, () => Ensure.NotNullOrEmpty(arg, nameof(arg)));
	}

	[Fact]
	public void NotNullOrEmptyDoesNotThrowOnNonEmptyString() {
		const string? arg = "not null";
		Ensure.NotNullOrEmpty(arg, nameof(arg));
	}

	[Fact]
	public void NotNullOrEmptyThrowsOnCollectionAsExpected() {
		ICollection<string>? collection = null;
		Assert.Throws<ArgumentNullException>(() => Ensure.NotNullOrEmpty(collection, nameof(collection)));
		collection = [];
		Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.NotNullOrEmpty(collection, nameof(collection)));
	}

	[Fact]
	public void NotNullOrEmptyDoesNotThrowOnNonEmptyCollection() {
		ICollection<string> collection = ["foo"];
		Ensure.NotNullOrEmpty(collection, nameof(collection));
	}

	[Fact]
	public void NotNullOrEmptyThrowsOnIEnumerableAsExpected() {
		IEnumerable<string>? collection = null;
		Assert.Throws<ArgumentNullException>(() => Ensure.NotNullOrEmpty(collection, nameof(collection)));
		collection = [];
		Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.NotNullOrEmpty(collection, nameof(collection)));
	}

	[Fact]
	public void NotNullOrEmptyDoesNotThrowOnNonEmptyIEnumerable() {
		IEnumerable<string> collection = ["foo"];
		Ensure.NotNullOrEmpty(collection, nameof(collection));
	}

	[Theory]
	[InlineData(null, typeof(ArgumentNullException))]
	[InlineData("", typeof(ArgumentException))]
	public void NotNullOrWhiteSpaceThrowsWhenItShould(string? arg, Type exceptionType) {
		Assert.Throws(exceptionType, () => Ensure.NotNullOrWhiteSpace(arg, nameof(arg)));
	}

	[Fact]
	public void NotNullOrWhiteSpaceDoesNotThrowWhenNotNullOrWhiteSpace() {
		const string? arg = "not null";
		Ensure.NotNullOrWhiteSpace(arg, nameof(arg));
	}

	[Theory]
	[InlineData(-1)]
	[InlineData(0)]
	public void PositiveThrowsForNegativeOrZeroInt(int number) {
		Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.Positive(number, nameof(number)));
	}

	[Fact]
	public void PositiveDoesNotThrowForPositiveInt() {
		const int number = 1;
		Ensure.Positive(number, nameof(number));
	}

	[Theory]
	[InlineData(-1)]
	[InlineData(0)]
	public void PositiveThrowsForNegativeOrZeroLong(long number) {
		Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.Positive(number, nameof(number)));
	}

	[Fact]
	public void PositiveDoesNotThrowForPositiveLong() {
		const long number = 1;
		Ensure.Positive(number, nameof(number));
	}

	[Theory]
	[InlineData(-1)]
	[InlineData(0)]
	public void PositiveThrowsForNegativeOrZeroDecimal(decimal number) {
		Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.Positive(number, nameof(number)));
	}

	[Fact]
	public void PositiveDoesNotThrowForPositiveDecimal() {
		const decimal number = 1;
		Ensure.Positive(number, nameof(number));
	}

	[Fact]
	public void NonnegativeThrowsForNegativeInt() {
		const int number = -1;
		Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.Nonnegative(number, nameof(number)));
	}

	[Fact]
	public void NonnegativeDoesNotThrowForNonnegativeInt() {
		const int number = 1;
		Ensure.Nonnegative(number, nameof(number));
	}

	[Fact]
	public void NonnegativeThrowsForNegativeOrZeroLong() {
		const long number = -1;
		Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.Nonnegative(number, nameof(number)));
	}

	[Fact]
	public void NonnegativeDoesNotThrowForNonnegativeLong() {
		const long number = 1;
		Ensure.Nonnegative(number, nameof(number));
	}

	[Fact]
	public void NonnegativeThrowsForNegativeOrZeroDecimal() {
		const decimal number = -1;
		Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.Nonnegative(number, nameof(number)));
	}

	[Fact]
	public void NonnegativeDoesNotThrowForNonnegativeDecimal() {
		const decimal number = 1;
		Ensure.Nonnegative(number, nameof(number));
	}

	[Fact]
	public void NotEmptyGuidThrowsOnEmptyGuid() {
		var id = Guid.Empty;
		Assert.Throws<ArgumentException>(() => Ensure.NotEmptyGuid(id, nameof(id)));
	}

	[Fact]
	public void NotEmptyGuidDoesNotThrowOnNonEmptyGuid() {
		var id = Guid.NewGuid();
		Ensure.NotEmptyGuid(id, nameof(id));
	}

	[Fact]
	public void EqualThrowsIfIntegersAreNotEqual() {
		const int a = 1;
		const int b = 2;
		Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.Equal(a, b, nameof(b)));
	}

	[Fact]
	public void EqualDoesNotThrowIfIntegersAreEqual() {
		const int a = 1;
		const int b = 1;
		Ensure.Equal(a, b, nameof(b));
	}

	[Fact]
	public void EqualThrowsIfLongsAreNotEqual() {
		const long a = 1;
		const long b = 2;
		Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.Equal(a, b, nameof(b)));
	}

	[Fact]
	public void EqualDoesNotThrowIfLongsAreEqual() {
		const long a = 1;
		const long b = 1;
		Ensure.Equal(a, b, nameof(b));
	}

	[Fact]
	public void EqualThrowsIfBooleansAreNotEqual() {
		const bool a = true;
		const bool b = false;
		Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.Equal(a, b, nameof(b)));
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void EqualDoesNotThrowIfBooleansAreEqual(bool value) {
		var a = value;
		var b = value;
		Ensure.Equal(a, b, nameof(b));
	}

	[Fact]
	public void EqualThrowsIfGuidsAreNotEqual() {
		var a = Guid.NewGuid();
		var b = Guid.NewGuid();
		Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.Equal(a, b, nameof(b)));
	}

	[Fact]
	public void EqualDoesNotThrowIfGuidsAreEqual() {
		var a = Guid.NewGuid();
		var b = a;
		Ensure.Equal(a, b, nameof(b));
	}

	[Fact]
	public void NotEqualThrowsIfIntegersAreEqual() {
		const int a = 1;
		const int b = 1;
		Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.NotEqual(a, b, nameof(b)));
	}

	[Fact]
	public void NotEqualDoesNotThrowIfIntegersAreNotEqual() {
		const int a = 1;
		const int b = 2;
		Ensure.NotEqual(a, b, nameof(b));
	}

	[Fact]
	public void NotEqualThrowsIfLongsAreEqual() {
		const long a = 1;
		const long b = 1;
		Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.NotEqual(a, b, nameof(b)));
	}

	[Fact]
	public void NotEqualDoesNotThrowIfLongsAreNotEqual() {
		const long a = 1;
		const long b = 2;
		Ensure.NotEqual(a, b, nameof(b));
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void NotEqualThrowsIfBooleansAreEqual(bool value) {
		var a = value;
		var b = value;
		Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.NotEqual(a, b, nameof(b)));
	}

	[Fact]
	public void NotEqualDoesNotThrowIfBooleansAreNotEqual() {
		const bool a = true;
		const bool b = false;
		Ensure.NotEqual(a, b, nameof(b));
	}

	[Fact]
	public void NotEqualThrowsIfGuidsAreEqual() {
		var a = Guid.NewGuid();
		var b = a;
		Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.NotEqual(a, b, nameof(b)));
	}

	[Fact]
	public void NotEqualDoesNotThrowIfGuidsAreNotEqual() {
		var a = Guid.NewGuid();
		var b = Guid.NewGuid();
		Ensure.NotEqual(a, b, nameof(b));
	}

	[Fact]
	public void PowerOf2ThrowsForNonPowerOf2Int() {
		const int value = 3;
		Assert.Throws<ArgumentException>(() => Ensure.PowerOf2(value, nameof(value)));
	}

	[Fact]
	public void PowerOf2DoesNotThrowForPowerOf2Int() {
		var i = 1;
		Ensure.PowerOf2(i, nameof(i));
		while (i <= int.MaxValue / 2) {
			i *= 2;
			Ensure.PowerOf2(i, nameof(i));
		}
	}

	[Theory]
	[InlineData(4, 4)]
	[InlineData(4, 3)]
	public void GreaterThanThrowsForOutOfRangeInt(int expected, int actual) {
		Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.GreaterThan(expected, actual, nameof(actual)));
	}

	[Fact]
	public void GreaterThanDoesNotThrowForInRangeInt() {
		const int expected = 4;
		const int actual = 5;
		Ensure.GreaterThan(expected, actual, nameof(actual));
	}

	[Theory]
	[InlineData(4, 4)]
	[InlineData(4, 3)]
	public void GreaterThanThrowsForOutOfRangeLong(long expected, long actual) {
		Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.GreaterThan(expected, actual, nameof(actual)));
	}

	[Fact]
	public void GreaterThanDoesNotThrowForInRangeLong() {
		const long expected = 4;
		const long actual = 5;
		Ensure.GreaterThan(expected, actual, nameof(actual));
	}

	[Theory]
	[InlineData(4, 4)]
	[InlineData(4, 3)]
	public void GreaterThanThrowsForOutOfRangeDecimal(decimal expected, decimal actual) {
		Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.GreaterThan(expected, actual, nameof(actual)));
	}

	[Fact]
	public void GreaterThanDoesNotThrowForInRangeDecimal() {
		const decimal expected = 4;
		const decimal actual = 5;
		Ensure.GreaterThan(expected, actual, nameof(actual));
	}

	[Fact]
	public void GreaterThanOrEqualToThrowsForOutOfRangeInt() {
		const int expected = 5;
		const int actual = 4;
		Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.GreaterThanOrEqualTo(expected, actual, nameof(actual)));
	}

	[Theory]
	[InlineData(4, 4)]
	[InlineData(4, 5)]
	public void GreaterThanOrEqualToDoesNotThrowForInRangeInt(int expected, int actual) {
		Ensure.GreaterThanOrEqualTo(expected, actual, nameof(actual));
	}

	[Fact]
	public void GreaterThanOrEqualToThrowsForOutOfRangeLong() {
		const long expected = 5;
		const long actual = 4;
		Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.GreaterThanOrEqualTo(expected, actual, nameof(actual)));
	}

	[Theory]
	[InlineData(4, 4)]
	[InlineData(4, 5)]
	public void GreaterThanOrEqualToDoesNotThrowForInRangeLong(long expected, long actual) {
		Ensure.GreaterThanOrEqualTo(expected, actual, nameof(actual));
	}

	[Fact]
	public void GreaterThanOrEqualToThrowsForOutOfRangeDecimal() {
		const decimal expected = 5;
		const decimal actual = 4;
		Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.GreaterThanOrEqualTo(expected, actual, nameof(actual)));
	}

	[Theory]
	[InlineData(4, 4)]
	[InlineData(4, 5)]
	public void GreaterThanOrEqualToDoesNotThrowForInRangeDecimal(decimal expected, decimal actual) {
		Ensure.GreaterThanOrEqualTo(expected, actual, nameof(actual));
	}

	[Theory]
	[InlineData(4, 4)]
	[InlineData(4, 5)]
	public void LessThanThrowsForOutOfRangeInt(int expected, int actual) {
		Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.LessThan(expected, actual, nameof(actual)));
	}

	[Fact]
	public void LessThanDoesNotThrowForInRangeInt() {
		const int expected = 5;
		const int actual = 4;
		Ensure.LessThan(expected, actual, nameof(actual));
	}

	[Theory]
	[InlineData(4, 4)]
	[InlineData(4, 5)]
	public void LessThanThrowsForOutOfRangeLong(long expected, long actual) {
		Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.LessThan(expected, actual, nameof(actual)));
	}

	[Fact]
	public void LessThanDoesNotThrowForInRangeLong() {
		const long expected = 5;
		const long actual = 4;
		Ensure.LessThan(expected, actual, nameof(actual));
	}

	[Theory]
	[InlineData(4, 4)]
	[InlineData(4, 5)]
	public void LessThanThrowsForOutOfRangeDecimal(decimal expected, decimal actual) {
		Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.LessThan(expected, actual, nameof(actual)));
	}

	[Fact]
	public void LessThanDoesNotThrowForInRangeDecimal() {
		const decimal expected = 5;
		const decimal actual = 4;
		Ensure.LessThan(expected, actual, nameof(actual));
	}

	[Fact]
	public void LessThanOrEqualToThrowsForOutOfRangeInt() {
		const int expected = 4;
		const int actual = 5;
		Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.LessThanOrEqualTo(expected, actual, nameof(actual)));
	}

	[Theory]
	[InlineData(4, 4)]
	[InlineData(4, 3)]
	public void LessThanOrEqualToDoesNotThrowForInRangeInt(int expected, int actual) {
		Ensure.LessThanOrEqualTo(expected, actual, nameof(actual));
	}

	[Fact]
	public void LessThanOrEqualToThrowsForOutOfRangeLong() {
		const long expected = 4;
		const long actual = 5;
		Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.LessThanOrEqualTo(expected, actual, nameof(actual)));
	}

	[Theory]
	[InlineData(4, 4)]
	[InlineData(4, 3)]
	public void LessThanOrEqualToDoesNotThrowForInRangeLong(long expected, long actual) {
		Ensure.LessThanOrEqualTo(expected, actual, nameof(actual));
	}

	[Fact]
	public void LessThanOrEqualToThrowsForOutOfRangeDecimal() {
		const decimal expected = 4;
		const decimal actual = 5;
		Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.LessThanOrEqualTo(expected, actual, nameof(actual)));
	}

	[Theory]
	[InlineData(4, 4)]
	[InlineData(4, 3)]
	public void LessThanOrEqualToDoesNotThrowForInRangeDecimal(decimal expected, decimal actual) {
		Ensure.LessThanOrEqualTo(expected, actual, nameof(actual));
	}

	[Theory]
	[InlineData(10, 20, 9)]
	[InlineData(10, 20, 10)]
	[InlineData(10, 20, 20)]
	public void BetweenThrowsWhenIntegerIsNotBetweenLimits(int low, int high, int actual) {
		Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.Between(low, high, actual, nameof(actual)));
	}

	[Fact]
	public void BetweenDoesNotThrowWhenIntegerIsBetweenLimits() {
		const int low = 10;
		const int high = 20;
		const int actual = 15;
		Ensure.Between(low, high, actual, nameof(actual));
	}

	[Theory]
	[InlineData(10, 20, 9)]
	[InlineData(10, 20, 10)]
	[InlineData(10, 20, 20)]
	public void BetweenThrowsWhenLongIsNotBetweenLimits(long low, long high, long actual) {
		Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.Between(low, high, actual, nameof(actual)));
	}

	[Fact]
	public void BetweenDoesNotThrowWhenLongIsBetweenLimits() {
		const long low = 10;
		const long high = 20;
		const long actual = 15;
		Ensure.Between(low, high, actual, nameof(actual));
	}

	[Theory]
	[InlineData(10, 20, 9)]
	[InlineData(10, 20, 10)]
	[InlineData(10, 20, 20)]
	public void BetweenThrowsWhenDecimalIsNotBetweenLimits(decimal low, decimal high, decimal actual) {
		Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.Between(low, high, actual, nameof(actual)));
	}

	[Fact]
	public void BetweenDoesNotThrowWhenDecimalIsBetweenLimits() {
		const decimal low = 10;
		const decimal high = 20;
		const decimal actual = 15;
		Ensure.Between(low, high, actual, nameof(actual));
	}

	[Theory]
	[InlineData(10, 20, 9)]
	[InlineData(10, 20, 21)]
	public void BetweenOrEqualThrowsWhenIntegerIsNotBetweenOrEqualToLimits(int low, int high, int actual) {
		Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.BetweenOrEqual(low, high, actual, nameof(actual)));
	}

	[Theory]
	[InlineData(10, 20, 15)]
	[InlineData(10, 20, 10)]
	[InlineData(10, 20, 20)]
	public void BetweenOrEqualDoesNotThrowWhenIntegerIsBetweenOrEqualLimits(int low, int high, int actual) {
		Ensure.BetweenOrEqual(low, high, actual, nameof(actual));
	}

	[Theory]
	[InlineData(10, 20, 9)]
	[InlineData(10, 20, 21)]
	public void BetweenOrEqualThrowsWhenLongIsNotBetweenOrEqualToLimits(long low, long high, long actual) {
		Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.BetweenOrEqual(low, high, actual, nameof(actual)));
	}

	[Theory]
	[InlineData(10, 20, 15)]
	[InlineData(10, 20, 10)]
	[InlineData(10, 20, 20)]
	public void BetweenOrEqualDoesNotThrowWhenLongIsBetweenOrEqualLimits(long low, long high, long actual) {
		Ensure.BetweenOrEqual(low, high, actual, nameof(actual));
	}

	[Theory]
	[InlineData(10, 20, 9)]
	[InlineData(10, 20, 21)]
	public void BetweenOrEqualThrowsWhenDecimalIsNotBetweenOrEqualToLimits(decimal low, decimal high, decimal actual) {
		Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.BetweenOrEqual(low, high, actual, nameof(actual)));
	}

	[Theory]
	[InlineData(10, 20, 15)]
	[InlineData(10, 20, 10)]
	[InlineData(10, 20, 20)]
	public void BetweenOrEqualDoesNotThrowWhenDecimalIsBetweenOrEqualLimits(decimal low, decimal high, decimal actual) {
		Ensure.BetweenOrEqual(low, high, actual, nameof(actual));
	}

	[Fact]
	public void NotDefaultThrowsForDefaultDateTime() {
		DateTime date = default;
		Assert.Throws<ArgumentException>(() => Ensure.NotDefault(date, nameof(date)));
	}

	[Fact]
	public void NotDefaultDoesNotThrowForNonDefaultDateTime() {
		var date = DateTime.Today;
		Ensure.NotDefault(date, nameof(date));
	}

	[Fact]
	public void ContainsKeyThrowsWhenKeyIsMissing() {
		Dictionary<string, string> lookup = new() { ["foo"] = "bar" };
		Assert.Throws<ArgumentException>(() => Ensure.ContainsKey(lookup, "baz", nameof(lookup)));
	}

	[Fact]
	public void ContainsKeyDoesNotThrowWhenKeyIsFound() {
		Dictionary<string, string> lookup = new() { ["foo"] = "bar" };
		Ensure.ContainsKey(lookup, "foo", nameof(lookup));
	}

	[Fact]
	public void DoesNotContainKeyThrowsWhenKeyIsPresent() {
		Dictionary<string, string> lookup = new() { ["foo"] = "bar" };
		Assert.Throws<ArgumentException>(() => Ensure.DoesNotContainKey(lookup, "foo", nameof(lookup)));
	}

	[Fact]
	public void DoesNotContainKeyDoesNotThrowWhenKeyIsMissing() {
		Dictionary<string, string> lookup = new() { ["foo"] = "bar" };
		Ensure.DoesNotContainKey(lookup, "baz", nameof(lookup));
	}

	[Fact]
	public void ContainsThrowsWhenItemIsMissing() {
		List<string> lookup = ["foo"];
		Assert.Throws<ArgumentException>(() => Ensure.Contains(lookup, "bar", nameof(lookup)));
	}

	[Fact]
	public void ContainsDoesNotThrowWhenItemIsPresent() {
		List<string> lookup = ["foo"];
		Ensure.Contains(lookup, "foo", nameof(lookup));
	}

	[Fact]
	public void DoesNotContainThrowsWhenItemIsPresent() {
		List<string> lookup = ["foo"];
		Assert.Throws<ArgumentException>(() => Ensure.DoesNotContain(lookup, "foo", nameof(lookup)));
	}

	[Fact]
	public void DoesNotContainDoesNotThrowWhenItemIsMissing() {
		List<string> lookup = ["foo"];
		Ensure.DoesNotContain(lookup, "bar", nameof(lookup));
	}

	[Fact]
	public void TrueThrowsWhenExpressionReturnsFalse() {
		Assert.Throws<ArgumentException>(() => Ensure.True(() => false, "arg"));
	}

	[Fact]
	public void TrueDoesNotThrowWhenExpressionReturnsTrue() {
		Ensure.True(() => true, "arg");
	}

	[Fact]
	public void FalseThrowsWhenExpressionReturnsTrue() {
		Assert.Throws<ArgumentException>(() => Ensure.False(() => true, "arg"));
	}

	[Fact]
	public void FalseDoesNotThrowWhenExpressionReturnsFalse() {
		Ensure.False(() => false, "arg");
	}
}
