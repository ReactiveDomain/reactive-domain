namespace ReactiveDomain.Util;

public static class StringExtensions {
	extension(string s) {
		public bool IsEmptyString() => string.IsNullOrEmpty(s);

		public bool IsNotEmptyString() => !string.IsNullOrEmpty(s);
	}
}
