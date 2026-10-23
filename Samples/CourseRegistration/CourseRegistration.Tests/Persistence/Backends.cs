using CourseRegistration.ReadModels;
using CourseRegistration.ReadModels.Persistence;
using Npgsql;
using Testcontainers.PostgreSql;

namespace CourseRegistration.Tests.Persistence;

/// <summary>
/// One Postgres server for the whole run. Each test gets a schema of its own, so tests share the
/// server and nothing else.
/// </summary>
/// <remarks>
/// Set <c>COURSE_REGISTRATION_POSTGRES</c> to a connection string to use a server already running;
/// its test schemas are dropped afterwards. Otherwise Testcontainers starts a throwaway one.
/// </remarks>
public sealed class PostgresFixture : IAsyncLifetime {
	private const string ServerVariable = "COURSE_REGISTRATION_POSTGRES";

	private readonly string? _server = Environment.GetEnvironmentVariable(ServerVariable);
	private readonly PostgreSqlContainer? _container;
	private readonly List<(string Schema, NpgsqlDataSource DataSource)> _databases = [];

	public PostgresFixture() {
		if (string.IsNullOrWhiteSpace(_server))
			_container = new PostgreSqlBuilder("postgres:16-alpine").Build();
	}

	private string ConnectionString => _container?.GetConnectionString() ?? _server!;

	public Task InitializeAsync() => _container?.StartAsync() ?? Task.CompletedTask;

	/// <summary>A data source on a new schema with every table created.</summary>
	public NpgsqlDataSource NewDatabase() {
		var dataSource = NewEmptyDatabase();
		ProjectionSchema.Create(dataSource);
		return dataSource;
	}

	/// <summary>A data source on a new schema with no tables in it.</summary>
	public NpgsqlDataSource NewEmptyDatabase() {
		var schema = $"t_{Guid.NewGuid():N}";
		Execute($"CREATE SCHEMA {schema}");
		var builder = new NpgsqlConnectionStringBuilder(ConnectionString) { SearchPath = schema };
		var dataSource = NpgsqlDataSource.Create(builder.ConnectionString);
		lock (_databases)
			_databases.Add((schema, dataSource));
		return dataSource;
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
