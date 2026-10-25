namespace ReactiveDomain.ReadModels;

/// <summary>Starts projections so that each one starts only once the owners of the tables it reads are live.</summary>
/// <remarks>
/// <para>A projection that reads another's table while catching up must see that table caught up too, or
/// it reads a partial history and bakes it into its own rows. Starting the owner first and waiting for
/// it to go live means the table holds at least everything written before the reader started.</para>
/// <para>That bounds catch-up only. Once both are live, they apply events independently, so a reader
/// must not depend on the owner having applied an event from the same moment.</para>
/// <para>An owner that stops instead of catching up — its <see cref="ProjectionReadModelBase.Fault"/>
/// set — fails its readers' start for the same reason: its table holds a partial history, and a
/// reader that loaded it would bake that history into rows its own checkpoints then stand behind.</para>
/// <para>Projections with no dependency between them start together.</para>
/// </remarks>
public static class ProjectionStartup {
	/// <summary>Starts every projection in dependency order. Completes once all are live.</summary>
	/// <exception cref="InvalidOperationException">
	/// Two projections own one table, a table read has no owner among <paramref name="projections"/>, or
	/// the reads form a cycle. Thrown before anything starts.
	/// </exception>
	public static Task StartAllAsync(IReadOnlyList<ProjectionReadModelBase> projections) {
		var owners = Owners(projections);
		var dependencies = projections.ToDictionary(p => p, p => DependenciesOf(p, owners));
		EnsureNoCycle(dependencies);

		var started = new Dictionary<ProjectionReadModelBase, Task>();
		Task Start(ProjectionReadModelBase projection) {
			if (started.TryGetValue(projection, out var task))
				return task;
			var owners = dependencies[projection];
			var waitFor = owners.Select(Start).ToArray();
			task = StartAfter(owners, waitFor, projection);
			started[projection] = task;
			return task;
		}
		return Task.WhenAll(projections.Select(Start));
	}

	/// <summary>The projection that owns each table, by row type.</summary>
	/// <exception cref="InvalidOperationException">Two projections own one table.</exception>
	public static IReadOnlyDictionary<Type, ProjectionReadModelBase> Owners(IEnumerable<ProjectionReadModelBase> projections) {
		var owners = new Dictionary<Type, ProjectionReadModelBase>();
		foreach (var projection in projections) {
			foreach (var row in ProjectionTables.Owned(projection.GetType())) {
				if (owners.TryGetValue(row, out var other))
					throw new InvalidOperationException(
						$"{projection.Name} and {other.Name} both own {row.Name}; a table has one owner.");
				owners[row] = projection;
			}
		}
		return owners;
	}

	private static async Task StartAfter(
		IReadOnlyList<ProjectionReadModelBase> owners, Task[] waitFor, ProjectionReadModelBase projection) {
		await Task.WhenAll(waitFor).ConfigureAwait(false);
		// StartAsync completes even when the projection stopped, so going live is not enough: an owner
		// that faulted mid-catch-up left a partial table, which a reader must not load.
		foreach (var owner in owners) {
			if (owner.Fault is { } fault)
				throw new InvalidOperationException(
					$"{projection.Name} reads tables of {owner.Name}, which stopped instead of catching up.", fault);
		}
		await projection.StartAsync().ConfigureAwait(false);
	}

	private static IReadOnlyList<ProjectionReadModelBase> DependenciesOf(
		ProjectionReadModelBase projection, IReadOnlyDictionary<Type, ProjectionReadModelBase> owners) =>
		ProjectionTables.Read(projection.GetType())
			.Select(row => owners.TryGetValue(row, out var owner)
				? owner
				: throw new InvalidOperationException($"{projection.Name} reads {row.Name}, which no projection owns."))
			.Where(owner => owner != projection)
			.Distinct()
			.ToList();

	private static void EnsureNoCycle(Dictionary<ProjectionReadModelBase, IReadOnlyList<ProjectionReadModelBase>> dependencies) {
		var done = new HashSet<ProjectionReadModelBase>();
		var path = new List<ProjectionReadModelBase>();
		void Visit(ProjectionReadModelBase projection) {
			if (done.Contains(projection))
				return;
			if (path.Contains(projection))
				throw new InvalidOperationException(
					$"Projections read each other's tables in a cycle: {string.Join(" -> ", path.SkipWhile(p => p != projection).Append(projection).Select(p => p.Name))}.");
			path.Add(projection);
			foreach (var owner in dependencies[projection])
				Visit(owner);
			path.RemoveAt(path.Count - 1);
			done.Add(projection);
		}
		foreach (var projection in dependencies.Keys)
			Visit(projection);
	}
}
