using CourseRegistration.Domain;
using CourseRegistration.ReadModels.Persistence;
using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging.Bus;

namespace CourseRegistration.ReadModels;

/// <summary>
/// Projects every section into <c>sections</c>, including how many seats are held: a counter, so it
/// is only right if each reservation and release is applied exactly once.
/// </summary>
[OwnedTables(typeof(SectionRow))]
public sealed class SectionCatalogRM :
	ProjectionRM,
	IHandle<SectionOffered>,
	IHandle<SeatReserved>,
	IHandle<SeatReleased>,
	IHandle<SectionRescheduled>,
	IHandle<SectionCapacityChanged>,
	IHandle<SectionCancelled> {
	private readonly IRowStore<Guid, SectionRow> _store;
	private readonly WriteBuffer<Guid, SectionRow> _sections;
	private readonly Dictionary<Guid, SectionRow> _rows = [];

	/// <summary>Creates the projection; call <see cref="ProjectionRM.StartAsync"/> to run it.</summary>
	public SectionCatalogRM(IConfiguredConnection connection, IProjectionDb db)
		: base(nameof(SectionCatalogRM), connection, db) {
		_store = db.Rows<Guid, SectionRow>();
		_sections = OwnTable(_store);
		EventStream.Subscribe<SectionOffered>(this);
		EventStream.Subscribe<SeatReserved>(this);
		EventStream.Subscribe<SeatReleased>(this);
		EventStream.Subscribe<SectionRescheduled>(this);
		EventStream.Subscribe<SectionCapacityChanged>(this);
		EventStream.Subscribe<SectionCancelled>(this);
	}

	/// <inheritdoc/>
	protected override void Load() {
		foreach (var row in _store.All())
			_rows[row.Id] = row;
	}

	/// <inheritdoc/>
	protected override IReadOnlyList<StreamStarter> Streams() => [Category<SectionAgg>()];

	void IHandle<SectionOffered>.Handle(SectionOffered e) {
		var row = new SectionRow {
			Id = e.SectionId,
			Term = e.Term,
			CourseCode = e.CourseCode,
			Department = SectionRow.DepartmentOf(e.CourseCode),
			SectionNumber = e.SectionNumber,
			Title = e.Title,
			Credits = e.Credits,
			Capacity = e.Capacity,
			Meetings = e.Meetings
		};
		_rows[row.Id] = row;
		_sections.Upsert(row.Id, row);
	}

	void IHandle<SeatReserved>.Handle(SeatReserved e) => Change(e.SectionId, r => r.SeatsHeld++);

	void IHandle<SeatReleased>.Handle(SeatReleased e) => Change(e.SectionId, r => r.SeatsHeld--);

	void IHandle<SectionRescheduled>.Handle(SectionRescheduled e) => Change(e.SectionId, r => r.Meetings = e.Meetings);

	void IHandle<SectionCapacityChanged>.Handle(SectionCapacityChanged e) => Change(e.SectionId, r => r.Capacity = e.Capacity);

	void IHandle<SectionCancelled>.Handle(SectionCancelled e) => Change(e.SectionId, r => {
		r.IsCancelled = true;
		r.SeatsHeld = 0;
	});

	// Every section event follows its SectionOffered on the same stream, so the row is always there.
	private void Change(Guid id, Action<SectionRow> change) {
		var row = _rows[id];
		change(row);
		_sections.Upsert(id, row);
	}
}
