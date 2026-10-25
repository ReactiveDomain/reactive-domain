namespace CourseRegistration.Domain;

internal static class Text {
	/// <summary>The trimmed value; throws when nothing but whitespace is left.</summary>
	public static string Required(string? value, string argumentName) {
		if (string.IsNullOrWhiteSpace(value))
			throw new ArgumentException("A value is required.", argumentName);
		return value.Trim();
	}
}
