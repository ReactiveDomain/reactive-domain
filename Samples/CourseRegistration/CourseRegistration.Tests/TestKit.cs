using CourseRegistration.Domain;
using CourseRegistration.ReadModels;
using CourseRegistration.ReadModels.Persistence;
using ReactiveDomain;
using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;
using ReactiveDomain.Testing.EventStore;

namespace CourseRegistration.Tests;

internal static class Times {
	public static readonly Meeting[] MonWed9 = [Meeting.At(DayOfWeek.Monday, "09:00", "09:50"), Meeting.At(DayOfWeek.Wednesday, "09:00", "09:50")];
	public static readonly Meeting[] MonWed930 = [Meeting.At(DayOfWeek.Monday, "09:30", "10:20"), Meeting.At(DayOfWeek.Wednesday, "09:30", "10:20")];
	public static readonly Meeting[] TueThu13 = [Meeting.At(DayOfWeek.Tuesday, "13:00", "14:15"), Meeting.At(DayOfWeek.Thursday, "13:00", "14:15")];
	public static readonly Meeting[] Fri15 = [Meeting.At(DayOfWeek.Friday, "15:00", "16:50")];
}

internal static class Aggregates {
	public static ICorrelatedMessage Source() => new CorrelationSource(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

	/// <summary>Takes the recorded events, and gives the aggregate a fresh source for the next change.</summary>
	public static object[] Take(this AggregateRoot aggregate) {
		var events = aggregate.TakeEvents();
		aggregate.Source = Source();
		return events;
	}

	/// <summary>Builds the aggregate the way a repository does: the private constructor, then the events.</summary>
	public static T Rehydrate<T>(IEnumerable<object> events) where T : AggregateRoot {
		var aggregate = (T)Activator.CreateInstance(typeof(T), nonPublic: true)!;
		aggregate.RestoreFromEvents(events);
		return aggregate;
	}

	public static SectionAgg Section(int capacity = 30, Meeting[]? meetings = null, string course = "CHEM 101", int credits = 4, Guid? id = null) =>
		new(id ?? Guid.NewGuid(), "2026FA", course, 1, "General Chemistry", credits, capacity, meetings ?? Times.MonWed9, Source());

	public static TermScheduleAgg Schedule(int maxCredits = 18, string term = "2026FA") =>
		new(Guid.NewGuid(), Guid.NewGuid(), term, maxCredits, Source());
}

/// <summary>
/// The whole system in one process: RD's in-memory stream store, a dispatcher, the registration
/// service, the process manager, and read models over whichever <see cref="IProjectionDb"/> a test passes.
/// </summary>
internal sealed class Harness : IDisposable {
	private readonly List<IDisposable> _owned = [];

	public MockStreamStoreConnection Store { get; }
	public IConfiguredConnection Connection { get; }
	public CorrelatedStreamStoreRepository Repository { get; }
	public Dispatcher Bus { get; }
	public IProjectionDb Db { get; }
	public RegistrationProcessRM? Process { get; private set; }

	public Harness(IProjectionDb? db = null, bool withProcess = true) {
		Store = new MockStreamStoreConnection(nameof(Harness));
		Store.Connect();
		Connection = new ConfiguredConnection(Store, new PrefixedCamelCaseStreamNameBuilder("reg"), new JsonMessageSerializer());
		Repository = new CorrelatedStreamStoreRepository(Connection.StreamNamer, Store, Connection.Serializer);
		Bus = Own(new Dispatcher(nameof(Harness), defaultAckTimeout: TestTimeouts.CommandTimeout, defaultResponseTimeout: TestTimeouts.CommandTimeout));
		Own(new RegistrationSvc(Repository).SubscribeTo(Bus));
		Db = db ?? new InMemoryProjectionDb();
		if (withProcess)
			StartProcess();
	}

	/// <summary>Starts a process manager, as an instance does on boot. A restart is a second call.</summary>
	public RegistrationProcessRM StartProcess(ICommandPublisher? bus = null) {
		Process?.Dispose();
		Process = new RegistrationProcessRM(Connection, bus ?? Bus);
		Wait(Process.StartAsync());
		return Process;
	}

	public SectionCatalogRM StartCatalog(IProjectionDb? db = null) => Start(new SectionCatalogRM(Connection, db ?? Db));

	public StudentTimetableRM StartTimetable(IProjectionDb? db = null) => Start(new StudentTimetableRM(Connection, db ?? Db));

	public SectionQueries Sections(IProjectionDb? db = null) => new((db ?? Db).ReadOnlyRows<Guid, SectionRow>());

	public TimetableQueries Timetables(IProjectionDb? db = null) {
		var source = db ?? Db;
		return new(source.ReadOnlyRows<Guid, ScheduleRow>(), source.ReadOnlyRows<Guid, TimetableEntryRow>());
	}

	public Guid Offer(string course = "CHEM 101", int number = 1, int capacity = 30, int credits = 4, Meeting[]? meetings = null,
		string term = "2026FA", string title = "General Chemistry") {
		var id = Guid.NewGuid();
		Send(new OfferSection(id, term, course, number, title, credits, capacity, meetings ?? Times.MonWed9));
		return id;
	}

	public Guid Open(int maxCredits = 18, string term = "2026FA", Guid? studentId = null) {
		var id = Guid.NewGuid();
		Send(new OpenTermSchedule(id, studentId ?? Guid.NewGuid(), term, maxCredits));
		return id;
	}

	public void Send(Command command) => Bus.Send(MessageBuilder.New(() => command));

	public TermScheduleAgg Schedule(Guid id) => Repository.GetById<TermScheduleAgg>(id, Aggregates.Source());

	public SectionAgg Section(Guid id) => Repository.GetById<SectionAgg>(id, Aggregates.Source());

	/// <summary>
	/// Waits until the process manager has seen every event written and has nothing open: every step
	/// it started has finished, and finishing it wrote nothing it has yet to see.
	/// </summary>
	public void Settle() => AssertEx.IsOrBecomesTrue(
		() => Process!.Idle && Bus.Idle && CaughtUp(Process) && Process.Open == (0, 0, 0),
		TestTimeouts.ThrottleWaitFor, "the registration process did not settle");

	/// <summary>Waits until <paramref name="model"/> has applied every event in both categories.</summary>
	public void CatchUp(ReadModelBase model) =>
		AssertEx.IsOrBecomesTrue(() => model.Idle && CaughtUp(model), TestTimeouts.ThrottleWaitFor, $"{model.GetType().Name} did not catch up");

	private bool CaughtUp(ReadModelBase model) {
		var delivered = model.GetCheckpoint().ToDictionary(c => c.StreamName, c => c.Version ?? -1);
		return delivered.All(c => c.Value == LastEventIn(c.Key));
	}

	private long LastEventIn(string stream) {
		try {
			return Store.ReadStreamForward(stream, 0, 4096).Events.Length - 1;
		} catch (StreamNotFoundException) {
			return -1;
		}
	}

	public static void Eventually(Func<bool> condition, string because) =>
		AssertEx.IsOrBecomesTrue(condition, TestTimeouts.ThrottleWaitFor, because);

	public static void Wait(Task task) => Assert.True(task.Wait(TestTimeouts.ThrottleWaitFor), "timed out starting");

	private T Start<T>(T model) where T : ProjectionRM {
		Own(model);
		Wait(model.StartAsync());
		return model;
	}

	public T Own<T>(T disposable) where T : IDisposable {
		_owned.Add(disposable);
		return disposable;
	}

	public void Dispose() {
		Process?.Dispose();
		for (var i = _owned.Count - 1; i >= 0; i--)
			_owned[i].Dispose();
		Store.Dispose();
	}
}
