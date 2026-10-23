using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using CourseRegistration.ReadModels.Persistence;
using Dapper;

namespace CourseRegistration.Tests.Persistence;

/// <summary>A table and columns named to collide: SQL keywords and acronym properties.</summary>
[Table("order")]
public sealed class OrderRow {
	[Key] public Guid Id { get; set; }
	public string User { get; set; } = "";
	public string URL { get; set; } = "";
	public string URLPath { get; set; } = "";
	public int SeatsHeld { get; set; }
}

// ReSharper disable once InconsistentNaming
public sealed class when_naming_columns {
	[Theory]
	[InlineData("SeatsHeld", "seats_held")]
	[InlineData("User", "user")]
	[InlineData("URL", "url")]
	[InlineData("URLPath", "url_path")]
	public void a_property_snake_cases_with_acronyms_kept_whole(string property, string column) =>
		Assert.Equal(column, RowMap<OrderRow>.Columns.Single(c => c.Property.Name == property).Name);
}

/// <summary>
/// Generated SQL quotes every identifier, so a property that snake-cases into a keyword, like
/// <c>User</c> or a table named <c>order</c>, is a name to Postgres and not a syntax error.
/// </summary>
[Collection(PostgresCollection.Name)]
// ReSharper disable once InconsistentNaming
public sealed class when_naming_columns_in_postgres(PostgresFixture postgres) {
	[Fact]
	public void keyword_table_and_column_names_round_trip() {
		var dataSource = postgres.NewEmptyDatabase();
		using (var connection = dataSource.OpenConnection())
			connection.Execute("""
				CREATE TABLE "order" (
					id         UUID    PRIMARY KEY,
					"user"     TEXT    NOT NULL,
					url        TEXT    NOT NULL,
					url_path   TEXT    NOT NULL,
					seats_held INTEGER NOT NULL
				)
				""");
		var db = new PostgresProjectionDb(dataSource);
		var store = db.Rows<Guid, OrderRow>();
		var row = new OrderRow { Id = Guid.NewGuid(), User = "pat", URL = "https://example.edu", URLPath = "/a", SeatsHeld = 1 };

		db.InTransaction(scope => store.Upsert(scope, row));
		row.SeatsHeld = 2;
		db.InTransaction(scope => store.Upsert(scope, row));

		Assert.Equal(("pat", 2), (store.Get(row.Id)!.User, store.Get(row.Id)!.SeatsHeld));
		Assert.Single(store.FindBy(Match.IgnoringCase("user", "PAT")));
		var page = store.Page(null, [new OrderBy("user")], 0, 10);
		Assert.Equal((1, 1), (page.Total, page.Items.Count));
		db.InTransaction(scope => store.Delete(scope, row.Id));
		Assert.Null(store.Get(row.Id));
	}
}
