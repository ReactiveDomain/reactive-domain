using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using System.Reflection;

namespace CourseRegistration.ReadModels.Persistence;

/// <summary>One mapped property of a row type.</summary>
public sealed record RowColumn(PropertyInfo Property, string Name, bool IsJson) {
	/// <summary>The SQL that writes this column's parameter, cast to <c>jsonb</c> where the column is one.</summary>
	public string Parameter => IsJson ? $"@{Property.Name}::jsonb" : $"@{Property.Name}";
}

/// <summary>
/// How a row type maps to its table: <c>[Table]</c> names it, <c>[Column]</c> names a column
/// (snake_case of the property otherwise), <c>[Key]</c> marks the key and <c>[NotMapped]</c> leaves a
/// property out.
/// </summary>
/// <remarks>
/// The Postgres and in-memory stores both resolve column names through this map, which is what
/// makes a filter that passes in an in-memory test name the same column in SQL.
/// </remarks>
public static class RowMap<TRow> {
	/// <summary>The table the rows are stored in.</summary>
	public static string Table { get; } =
		typeof(TRow).GetCustomAttribute<TableAttribute>()?.Name
		?? throw new InvalidOperationException($"{typeof(TRow).Name} has no [Table] attribute.");

	/// <summary>Every mapped column, in declaration order.</summary>
	public static IReadOnlyList<RowColumn> Columns { get; } = typeof(TRow)
		.GetProperties(BindingFlags.Public | BindingFlags.Instance)
		.Where(p => p.GetCustomAttribute<NotMappedAttribute>() is null && p.CanRead && p.CanWrite)
		.Select(p => {
			var column = p.GetCustomAttribute<ColumnAttribute>();
			return new RowColumn(p, column?.Name ?? SnakeCase(p.Name),
				string.Equals(column?.TypeName, "jsonb", StringComparison.OrdinalIgnoreCase));
		})
		.ToList();

	/// <summary>The key column.</summary>
	public static RowColumn Key { get; } =
		Columns.SingleOrDefault(c => c.Property.GetCustomAttribute<KeyAttribute>() is not null)
		?? throw new InvalidOperationException($"{typeof(TRow).Name} needs exactly one [Key] property.");

	/// <summary><c>column AS Property</c> for every column, so Dapper maps snake_case onto the properties.</summary>
	public static string SelectList { get; } = string.Join(", ", Columns.Select(c => $"{c.Name} AS {c.Property.Name}"));

	private static readonly Dictionary<string, RowColumn> ByName =
		Columns.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);

	/// <summary>The column named <paramref name="name"/>.</summary>
	/// <exception cref="ArgumentException">No such column: a typo is caught by an in-memory test rather than by Postgres.</exception>
	public static RowColumn Column(string name) =>
		ByName.TryGetValue(name, out var column)
			? column
			: throw new ArgumentException($"{typeof(TRow).Name} has no column '{name}'.", nameof(name));

	/// <summary>The value <paramref name="row"/> holds in <paramref name="column"/>.</summary>
	public static object? Value(TRow row, string column) => Column(column).Property.GetValue(row);

	/// <summary>A copy holding the same column values, so a stored row is never shared with a caller.</summary>
	/// <remarks>Array columns are copied too: an array is the one mutable value a row holds.</remarks>
	public static TRow Copy(TRow row) {
		var copy = Activator.CreateInstance<TRow>();
		foreach (var column in Columns) {
			var value = column.Property.GetValue(row);
			column.Property.SetValue(copy, value is Array array ? array.Clone() : value);
		}
		return copy;
	}

	private static string SnakeCase(string name) =>
		string.Concat(name.Select((c, i) =>
			i > 0 && char.IsUpper(c)
				? "_" + char.ToLower(c, CultureInfo.InvariantCulture)
				: char.ToLower(c, CultureInfo.InvariantCulture).ToString()));
}
