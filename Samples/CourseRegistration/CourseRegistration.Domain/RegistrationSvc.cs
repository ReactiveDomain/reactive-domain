using ReactiveDomain;
using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;

namespace CourseRegistration.Domain;

/// <summary>
/// Handles every registration command. Each handler loads one aggregate, calls one method and saves;
/// the steps that span a section and a schedule are chained by <see cref="RegistrationProcessRM"/>.
/// </summary>
public sealed class RegistrationSvc :
	IHandleCommand<OfferSection>,
	IHandleCommand<RescheduleSection>,
	IHandleCommand<ChangeSectionCapacity>,
	IHandleCommand<CancelSection>,
	IHandleCommand<OpenTermSchedule>,
	IHandleCommand<RequestCourse>,
	IHandleCommand<RequestSwap>,
	IHandleCommand<DropCourse>,
	IHandleCommand<CloseAddDrop>,
	IHandleCommand<AddReservedCourse>,
	IHandleCommand<SwapReservedCourse>,
	IHandleCommand<ReleaseSeat>,
	IHandleCommand<RemoveCancelledCourse>,
	IDisposable {
	// Two writers on one stream: the loser reloads and decides again. Enough for contention on one
	// popular section; not a substitute for a queue in front of a flash sale.
	private const int SaveAttempts = 5;

	private readonly ICorrelatedRepository _repository;
	private readonly List<IDisposable> _subscriptions = [];

	/// <summary>Creates the service over <paramref name="repository"/>.</summary>
	public RegistrationSvc(ICorrelatedRepository repository) {
		_repository = repository;
	}

	/// <summary>Subscribes every command this service handles on <paramref name="dispatcher"/>.</summary>
	public RegistrationSvc SubscribeTo(ICommandSubscriber dispatcher) {
		_subscriptions.Add(dispatcher.Subscribe<OfferSection>(this));
		_subscriptions.Add(dispatcher.Subscribe<RescheduleSection>(this));
		_subscriptions.Add(dispatcher.Subscribe<ChangeSectionCapacity>(this));
		_subscriptions.Add(dispatcher.Subscribe<CancelSection>(this));
		_subscriptions.Add(dispatcher.Subscribe<OpenTermSchedule>(this));
		_subscriptions.Add(dispatcher.Subscribe<RequestCourse>(this));
		_subscriptions.Add(dispatcher.Subscribe<RequestSwap>(this));
		_subscriptions.Add(dispatcher.Subscribe<DropCourse>(this));
		_subscriptions.Add(dispatcher.Subscribe<CloseAddDrop>(this));
		_subscriptions.Add(dispatcher.Subscribe<AddReservedCourse>(this));
		_subscriptions.Add(dispatcher.Subscribe<SwapReservedCourse>(this));
		_subscriptions.Add(dispatcher.Subscribe<ReleaseSeat>(this));
		_subscriptions.Add(dispatcher.Subscribe<RemoveCancelledCourse>(this));
		return this;
	}

	public CommandResponse Handle(OfferSection cmd) {
		if (_repository.TryGetById<SectionAgg>(cmd.SectionId, out _, cmd))
			return cmd.Fail(new InvalidOperationException($"Section {cmd.SectionId} already exists."));
		var section = new SectionAgg(
			cmd.SectionId, cmd.Term, cmd.CourseCode, cmd.SectionNumber, cmd.Title, cmd.Credits, cmd.Capacity, cmd.Meetings, cmd);
		return cmd.Succeed(_repository.Save(section));
	}

	public CommandResponse Handle(OpenTermSchedule cmd) {
		if (_repository.TryGetById<TermScheduleAgg>(cmd.ScheduleId, out _, cmd))
			return cmd.Fail(new InvalidOperationException($"Schedule {cmd.ScheduleId} already exists."));
		var schedule = new TermScheduleAgg(cmd.ScheduleId, cmd.StudentId, cmd.Term, cmd.MaxCredits, cmd);
		return cmd.Succeed(_repository.Save(schedule));
	}

	public CommandResponse Handle(RescheduleSection cmd) =>
		Change<SectionAgg>(cmd, cmd.SectionId, s => s.Reschedule(cmd.Meetings));

	public CommandResponse Handle(ChangeSectionCapacity cmd) =>
		Change<SectionAgg>(cmd, cmd.SectionId, s => s.ChangeCapacity(cmd.Capacity));

	public CommandResponse Handle(CancelSection cmd) =>
		Change<SectionAgg>(cmd, cmd.SectionId, s => s.Cancel());

	// The schedule is read only to prove it exists; the reservation is the section's decision.
	public CommandResponse Handle(RequestCourse cmd) {
		if (!_repository.TryGetById<TermScheduleAgg>(cmd.ScheduleId, out _, cmd))
			return cmd.Fail(new AggregateNotFoundException(cmd.ScheduleId, typeof(TermScheduleAgg)));
		return Change<SectionAgg>(cmd, cmd.SectionId, s => s.Reserve(cmd.ScheduleId));
	}

	public CommandResponse Handle(RequestSwap cmd) {
		if (!_repository.TryGetById<TermScheduleAgg>(cmd.ScheduleId, out _, cmd))
			return cmd.Fail(new AggregateNotFoundException(cmd.ScheduleId, typeof(TermScheduleAgg)));
		return Change<SectionAgg>(cmd, cmd.AddSectionId, s => s.Reserve(cmd.ScheduleId, cmd.DropSectionId));
	}

	public CommandResponse Handle(DropCourse cmd) =>
		Change<TermScheduleAgg>(cmd, cmd.ScheduleId, s => s.Drop(cmd.SectionId));

	public CommandResponse Handle(CloseAddDrop cmd) =>
		Change<TermScheduleAgg>(cmd, cmd.ScheduleId, s => s.Close());

	public CommandResponse Handle(AddReservedCourse cmd) =>
		Change<TermScheduleAgg>(cmd, cmd.ScheduleId,
			s => s.Add(cmd.SectionId, cmd.Term, cmd.CourseCode, cmd.Credits, cmd.Meetings));

	public CommandResponse Handle(SwapReservedCourse cmd) =>
		Change<TermScheduleAgg>(cmd, cmd.ScheduleId,
			s => s.Swap(cmd.DropSectionId, cmd.AddSectionId, cmd.Term, cmd.CourseCode, cmd.Credits, cmd.Meetings));

	public CommandResponse Handle(ReleaseSeat cmd) =>
		Change<SectionAgg>(cmd, cmd.SectionId, s => s.Release(cmd.ScheduleId));

	public CommandResponse Handle(RemoveCancelledCourse cmd) =>
		Change<TermScheduleAgg>(cmd, cmd.ScheduleId, s => s.RemoveCancelled(cmd.SectionId));

	private CommandResponse Change<TAggregate>(Command cmd, Guid id, Action<TAggregate> change)
		where TAggregate : AggregateRoot {
		for (var attempt = 1; ; attempt++) {
			try {
				var aggregate = _repository.GetById<TAggregate>(id, cmd);
				change(aggregate);
				return cmd.Succeed(_repository.Save(aggregate));
			} catch (WrongExpectedVersionException) when (attempt < SaveAttempts) {
				// Another writer got there first; reload and decide against what it wrote.
			} catch (Exception ex) {
				return cmd.Fail(ex);
			}
		}
	}

	public void Dispose() {
		_subscriptions.ForEach(s => s.Dispose());
		_subscriptions.Clear();
	}
}
