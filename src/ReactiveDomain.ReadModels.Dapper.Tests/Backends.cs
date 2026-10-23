using Dapper;
using Npgsql;
using Testcontainers.PostgreSql;

namespace ReactiveDomain.ReadModels.Tests;

/// <summary>
/// One Postgres server for the whole run. Each test gets a schema of its own, so tests share the
/// server and nothing else.
/// </summary>
/// <remarks>
/// Set <c>RD_READMODELS_TESTS_POSTGRES</c> to a connection string to use a server already running;
/// its test schemas are dropped afterwards. Otherwise Testcontainers starts a throwaway server, and
/// where no Docker that can run Linux containers is reachable — the Windows CI runner — every test
/// that asks for a database is skipped, never silently passed.
/// </remarks>
public sealed class PostgresFixture : IAsyncLifetime {
	private const string ServerVariable = "RD_READMODELS_TESTS_POSTGRES";

	private readonly string? _server = Environment.GetEnvironmentVariable(ServerVariable);
	private readonly List<(string Schema, NpgsqlDataSource DataSource)> _databases = [];
	private PostgreSqlContainer? _container;
	private string? _unavailable;

	private string ConnectionString => _container?.GetConnectionString() ?? _server!;

	public async Task InitializeAsync() {
		if (!string.IsNullOrWhiteSpace(_server))
			return;
		try {
			_container = new PostgreSqlBuilder("postgres:16-alpine").Build();
			await _container.StartAsync();
		} catch (Exception ex) {
			_unavailable = $"Postgres tests need Docker or {ServerVariable}: {ex.Message.Split('\n')[0]}";
			_container = null;
		}
	}

	/// <summary>A data source on a new schema with every table created, or a skipped test where no server is reachable.</summary>
	public NpgsqlDataSource NewDatabase() {
		Skip.If(_unavailable is not null, _unavailable);
		var schema = $"t_{Guid.NewGuid():N}";
		Execute($"CREATE SCHEMA {schema}");
		var builder = new NpgsqlConnectionStringBuilder(ConnectionString) { SearchPath = schema };
		var dataSource = NpgsqlDataSource.Create(builder.ConnectionString);
		lock (_databases)
			_databases.Add((schema, dataSource));
		ProjectionSchema.Create(dataSource);
		CreateTestTables(dataSource);
		return dataSource;
	}

	// The row tables the tests fill; their DDL agrees with each row type's RowMap.
	private static void CreateTestTables(NpgsqlDataSource dataSource) {
		using var connection = dataSource.OpenConnection();
		connection.Execute("""
			CREATE TABLE "order" (
				id     UUID      PRIMARY KEY,
				"user" TEXT      NOT NULL,
				url    TEXT      NOT NULL,
				credit INTEGER   NOT NULL,
				tags   JSONB     NOT NULL,
				sizes  INTEGER[] NOT NULL
			);

			CREATE TABLE thing_counts (
				id    UUID    PRIMARY KEY,
				total INTEGER NOT NULL
			);
			""");
	}

	public async Task DisposeAsync() {
		foreach (var (schema, dataSource) in _databases) {
			await dataSource.DisposeAsync();
			if (_container is null)
				Execute($"DROP SCHEMA IF EXISTS {schema} CASCADE");
		}
		if (_container is not null)
			await _container.DisposeAsync();
	}

	private void Execute(string sql) {
		using var admin = NpgsqlDataSource.Create(ConnectionString);
		using var command = admin.CreateCommand(sql);
		command.ExecuteNonQuery();
	}
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture> {
	public const string Name = "postgres";
}

/// <summary>Throws before committing the next transactions it is told to, after their work has run, so nothing they wrote is kept.</summary>
internal sealed class FaultyDb(IProjectionDb inner) : IProjectionDb {
	private int _throwsLeft;

	public int Commits { get; private set; }

	public void ThrowBeforeNextCommit(int count = 1) => Interlocked.Exchange(ref _throwsLeft, count);

	public IRowStore<TKey, TRow> Rows<TKey, TRow>() where TKey : notnull where TRow : class, new() => inner.Rows<TKey, TRow>();

	public ICheckpointStore Checkpoints => inner.Checkpoints;

	public IProjectionWriters Writers => inner.Writers;

	public void InTransaction(Action<IWriteScope> work) {
		inner.InTransaction(scope => {
			work(scope);
			if (Interlocked.Decrement(ref _throwsLeft) >= 0)
				throw new InvalidOperationException("thrown before commit");
			Interlocked.Exchange(ref _throwsLeft, 0);
		});
		Commits++;
	}
}
