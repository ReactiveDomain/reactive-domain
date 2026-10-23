# Course registration sample

College course registration with add/drop, built on ReactiveDomain, with its read side in Postgres
through Dapper. It shows how an RD read model persists rows and checkpoints together, resumes from
them, and is tested against both an in-memory store and a real Postgres.

The Dapper code here is self-contained, a first cut of what RD's own Dapper read-model support should
offer. Nothing in it is a published package.

## The domain

Two invariants pull against each other: a section has a fixed number of seats, and a student has a
credit limit, no time conflicts and no course twice. Each has one owner.

| Aggregate | Owns |
|---|---|
| `SectionAgg` | Capacity. A seat is held by a schedule id; a reservation *is* the seat. Its id comes from its catalog name, so a section is offered once. |
| `TermScheduleAgg` | One student's term: credit limit, time conflicts, no duplicate course, the add/drop window. |

The schedule sits between the student and the course. A course is added in two steps, chained by
`RegistrationProcessRM`:

1. **Reserve.** The section holds a seat (`SeatReserved`) or refuses (`SeatRefused`).
2. **Commit or release.** The schedule checks its own rules against the reserved section. It either
   adds the course (`CourseAdded`) or turns it down (`CourseAddRejected`). If it turns it down, the
   seat is released (`SeatReleased`).

A drop releases the seat afterwards. A swap reserves the new seat first and gives up the old one only
once it holds the new one. Cancelling a section releases every seat and takes the section off every
schedule.

Both rules hold at all times. The only gap is a seat reserved before the schedule has answered, which
is never a broken rule. The process manager always closes it. It sends without waiting for the handler,
and a step stays open until the event that closes it arrives, so any step open longer than the retry
delay is sent again, whatever happened to the command. Give it a dispatcher with at least one publish
queue; with none, RD runs the handler on the sending thread. After a restart it replays without
sending anything, then resends whatever is still open.

## Where each Dapper usage is

| Usage | Code | Tests |
|---|---|---|
| Rows mapped by `[Table]`/`[Column]`/`[Key]`/`[NotMapped]`; `col AS Prop` select lists; `ON CONFLICT` upsert and insert | `Persistence/RowMap.cs`, `Persistence/PostgresProjectionDb.cs` | `when_storing_rows` |
| `jsonb` columns: `@Prop::jsonb` on write, queried with `jsonb_array_elements` | `Rows.cs`, `Queries.cs` | `a_jsonb_column_round_trips`, `a_start_time_keeps_…` |
| Every write in a transaction scope; work that throws keeps nothing | `IProjectionDb.InTransaction` | `a_transaction_that_throws_keeps_none_…` |
| Rows and checkpoints in one transaction, through RD's `BufferedReadModelBase`/`WriteBuffer` | `ProjectionRM.cs` | `when_projecting_registrations` |
| Resuming from saved checkpoints with no double count; checkpoints that never move back and skip unapplied streams | `ProjectionRM.StartAsync`, `PostgresCheckpointStore` | `a_restarted_catalog_resumes_…`, `checkpoints_are_kept_per_stream_…` |
| A write that throws: nothing committed, retried by the next flush, replayed after a restart | `ProjectionRM.Flush` | `a_write_that_throws_saves_neither_…`, `a_restart_after_a_write_that_threw_…` |
| A handler that throws stops the projection: its buffered work is dropped, nothing later is applied, and the checkpoint stays at the last written event | `ProjectionRM` (`EventStream`, `Fault`) | `a_handler_that_throws_while_live_…`, `a_handler_that_throws_while_catching_up_…` |
| Multi-stream projection: one checkpoint per category, no cross-stream ordering assumed, one-to-many fan-out | `StudentTimetableRM.cs` | `the_timetable_saves_a_checkpoint_…`, `caught_up_from_scratch_…`, `rescheduling_a_section_…` |
| Ownership: `[OwnedTables]` and `[ReadsTables]` declare each projection's tables; an `IRowStore` only for owned ones, `IReadOnlyRowStore` for the rest | `ProjectionRM.OwnTable`/`ReadTable`, `Queries.cs` | `writing_or_reading_an_undeclared_table_…`, `two_projections_owning_one_table_…` |
| Start order: a projection starts once the owners of the tables it reads are live; missing owners and cycles are refused before anything starts | `ProjectionStartup` | `a_reader_starts_only_once_the_owner_…`, `projections_reading_each_other_in_a_cycle_…` |
| Natural keys: case-insensitive lookup served by a `UNIQUE (UPPER(col))` index; `FindOne` throws on two matches | `ProjectionSchema.cs`, `SectionQueries.Find` | `the_natural_key_index_refuses_…`, `two_rows_on_one_natural_key_…` |
| `= ANY(@Values)` with a typed array | `PostgresRowStore.FindIn` | `finding_by_many_values_…` |
| Array columns (`INTEGER[]`, `UUID[]`) with GIN indexes: overlap (`&&`) and membership (`@x = ANY(col)`) | `SectionRow.MeetingDays`, `ScheduleRow.SectionIds`, `SectionQueries.Filter`, `TimetableQueries.SchedulesWith` | `array_columns_round_trip_…`, `avoided_days_drop_…`, `the_schedules_with_a_section_…` |
| Schema evolution: tables created in their first shape, then `ADD COLUMN IF NOT EXISTS` steps with backfills, so an existing database upgrades in place | `ProjectionSchema.cs` | `a_first_release_database_upgrades_in_place_…` |
| Rebuilds: a reset key empties projection tables and checkpoints once, claimed in the same transaction so one instance does it; or one projection alone | `ProjectionReset` | `a_reset_key_rebuilds_the_projections_once_…`, `instances_starting_together_…`, `rebuilding_one_projection_…` |
| One filter written as SQL plus a matching predicate, paged with a stable order (`COLLATE "C"`, key last) | `RowFilter`, `SectionQueries.Filter` | `when_searching_sections` (both stores), `a_page_sorts_text_by_code_point_…` |
| Idempotent ledgers: `INSERT … ON CONFLICT DO NOTHING` decides alone (no check-then-insert) | `ReminderLedger`, `CommandAuditLog` | `racing_recorders_send_one_reminder`, `the_audit_log_traces_…` |
| Command audit with correlation and causation, `jsonb` body; an audit outage does not stop commands | `CommandAuditLog`, `AuditingCommandPublisher` | `the_audit_log_traces_…`, `a_command_is_sent_even_when_the_audit_…` |
| One instance per scheduled window, claimed by primary key with a lease; a window is completed only when every step succeeded | `JobClaims`, `AddDropJobs` | `one_instance_claims_each_window_…`, `an_unfinished_claim_is_taken_over_…`, `a_close_job_with_unsent_closes_…` |
| The same test class against both stores; a Postgres schema per test | `Persistence/Backends.cs` | every `…_in_memory`/`…_in_postgres` pair |

## Running it

```bash
dotnet test Samples/CourseRegistration/CourseRegistration.sln
```

The `…_in_postgres` test classes start `postgres:16-alpine` with Testcontainers, so they need a reachable
Docker-API socket. With Podman, point Testcontainers at the machine's socket and turn off its reaper
container:

```bash
export DOCKER_HOST="unix://$(podman machine inspect --format '{{.ConnectionInfo.PodmanSocket.Path}}')"
export TESTCONTAINERS_RYUK_DISABLED=true
```

To use a Postgres server that is already running instead, give the tests a connection string. They
create a schema per test and drop it afterwards, so point them at a scratch database, not one you
work in:

```bash
export COURSE_REGISTRATION_POSTGRES="Host=127.0.0.1;Port=5432;Database=course_registration_tests;Username=postgres"
```

Every other test runs in memory and needs nothing:

```bash
dotnet test Samples/CourseRegistration/CourseRegistration.sln --filter "FullyQualifiedName!~_in_postgres"
```

## What a store in memory cannot show

`InMemoryProjectionDb` keeps the same contract, transactions included. It runs each filter's predicate
half and ignores the SQL, and it enforces no unique index. That is why every contract also runs against
Postgres: a SQL half that drifts from its predicate fails there.
