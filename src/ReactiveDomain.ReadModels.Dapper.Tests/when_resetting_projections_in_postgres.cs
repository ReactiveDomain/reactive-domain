using Npgsql;

namespace ReactiveDomain.ReadModels.Tests;

/// <summary>Rebuilds: reset keys claimed once, one projection rebuilt alone. Postgres only, as the reset is.</summary>
[Collection(PostgresCollection.Name)]
// ReSharper disable once InconsistentNaming
public sealed class when_resetting_projections_in_postgres(PostgresFixture postgres) {
	private static void Seed(IProjectionDb db, string projection, Guid thing) {
		var store = db.Rows<Guid, ThingCountRow>();
		db.InTransaction(scope => {
			store.Upsert(scope, new ThingCountRow { Id = thing, Total = 1 });
			db.Checkpoints.Save(scope, projection, [new StreamCheckpoint("s", 3)]);
		});
	}

	[SkippableFact]
	public void a_reset_key_rebuilds_the_projections_once_and_a_new_key_rebuilds_again() {
		var dataSource = postgres.NewDatabase();
		var db = new PostgresProjectionDb(dataSource);
		var thing = Guid.NewGuid();
		Seed(db, nameof(CountsRM), thing);

		Assert.True(ProjectionReset.ResetOnce(dataSource, "deploy-1", typeof(CountsRM)));
		Assert.Null(db.Rows<Guid, ThingCountRow>().Get(thing));
		Assert.Empty(db.Checkpoints.Load(nameof(CountsRM)));

		Seed(db, nameof(CountsRM), thing);
		Assert.False(ProjectionReset.ResetOnce(dataSource, "deploy-1", typeof(CountsRM)));
		Assert.NotNull(db.Rows<Guid, ThingCountRow>().Get(thing));

		Assert.True(ProjectionReset.ResetOnce(dataSource, "deploy-2", typeof(CountsRM)));
		Assert.Null(db.Rows<Guid, ThingCountRow>().Get(thing));
	}

	[SkippableFact]
	public void rebuilding_one_projection_leaves_the_others_checkpoints_alone() {
		var dataSource = postgres.NewDatabase();
		var db = new PostgresProjectionDb(dataSource);
		var thing = Guid.NewGuid();
		Seed(db, nameof(CountsRM), thing);
		db.InTransaction(scope => db.Checkpoints.Save(scope, "other", [new StreamCheckpoint("s", 7)]));

		ProjectionReset.Rebuild(dataSource, typeof(CountsRM));

		Assert.Null(db.Rows<Guid, ThingCountRow>().Get(thing));
		Assert.Empty(db.Checkpoints.Load(nameof(CountsRM)));
		Assert.Single(db.Checkpoints.Load("other"));
	}

	[SkippableFact]
	public void creating_the_schema_again_changes_nothing() {
		var dataSource = postgres.NewDatabase();
		var db = new PostgresProjectionDb(dataSource);
		db.InTransaction(scope => db.Checkpoints.Save(scope, "p", [new StreamCheckpoint("s", 1)]));

		ProjectionSchema.Create(dataSource);

		Assert.Single(db.Checkpoints.Load("p"));
	}
}
