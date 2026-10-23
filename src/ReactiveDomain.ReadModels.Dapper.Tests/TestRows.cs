using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ReactiveDomain.ReadModels.Tests;

/// <summary>
/// The contract row. Its table and a column are SQL keywords and a property is an acronym, so every
/// test that touches it also pins the identifier quoting and the snake-casing.
/// </summary>
[Table("order")]
public sealed class OrderRow {
	[Key] public Guid Id { get; set; }
	public string User { get; set; } = string.Empty;
	public string URL { get; set; } = string.Empty;
	public int Credit { get; set; }
	[Column("tags", TypeName = "jsonb")] public string TagsJson { get; set; } = "[]";
	public int[] Sizes { get; set; } = [];
}

/// <summary>What <see cref="CountsRM"/> keeps per thing.</summary>
[Table("thing_counts")]
public sealed class ThingCountRow {
	[Key] public Guid Id { get; set; }
	public int Total { get; set; }
}

/// <summary>What the startup-order tests' reading projection owns.</summary>
[Table("thing_summaries")]
public sealed class ThingSummaryRow {
	[Key] public Guid Id { get; set; }
	public int Things { get; set; }
}
