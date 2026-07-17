# Storage Update: Test Infrastructure Plan

Replace `MockStreamStoreConnection` as the backing store for `ReactiveDomain.Testing` with
the SQLite StreamStore, connected through the gRPC wrapper described in
[streamstore-storage-port.md](streamstore-storage-port.md). The real store reproduces async
delivery, so tests exercise the same timing behaviors as production instead of the mock's
synchronous shortcut.

The work splits across two releases:

- **0.15.2** ships every primitive and pattern. All of it is backend-independent and (with
  one flagged exception) purely additive, so consumers — including RD's own test suite —
  migrate their tests incrementally against the unchanged synchronous mock.
- **0.16.0** ships the wrapper and flips the backend. Tests already written fence-then-assert
  keep passing; only genuinely racy stragglers surface.

The designs below are proven — they run today in a consuming application's CI against the
real store.

## Tracking

**0.15.2** — #223 (TestQueue signal-before-filter), #224 (AwaitEventDelivery fence),
#225 (CatchUpConnection), #226 (FaultInjectingConfiguredConnection), #227 (CI-aware timeouts),
#228 (ProjectedEvent tagging in EventStoreConnectionWrapper), #229 (IsLive contract docs).

**0.16.0** — #230 (gRPC StreamStore wrapper — see
[streamstore-storage-port.md](streamstore-storage-port.md)), #231 (spec-base backend swap),
#232 (residual test migration), #233 (remove MockStreamStoreConnection), #234 (IsLive
alignment), #235 (Testing.Host packaging — see storage-port doc).

### Implementation checklist — 0.15.2

Sequential, one PR per issue against `master`, in this order (fence depends on the TestQueue
change and the timeout source). Each PR ticks its box here, adds targeted tests in the
matching test project, and closes its issue (`Closes #NNN`); CI is the full gate. No version
bump, no publishing.

- [x] **#223 TestQueue** — signal `_idWatchList` waiters before the type filter, **and**
  record ids that arrive with no watcher registered (insert an already-set wait handle) —
  without this the fence deterministically times out on the synchronous mock (see § 0.15.2/1).
  Same pass: `WaitForMultiple<T>` full-queue re-snapshot per iteration, `is T` (wait) vs
  `IsAssignableFrom` (ingest), unsynchronized `_handledTypes`.
- [x] **#227 TestTimeouts** — static source keyed on `GITHUB_ACTIONS` (WaitFor 500 ms/5 s,
  CommandTimeout 500 ms/10 s, ThrottleWaitFor 2 s/10 s) plus `MaxCpuCount=1` runsettings and
  `start /affinity` + `GITHUB_ACTIONS=true` repro guidance. Before #224 — the fence uses
  `TestTimeouts.WaitFor`.
- [x] **#224 AwaitEventDelivery fence** — `SetupStartMarker`/`DeliveryFence` derive from
  `Message` (a record), not `Event`; stream `setupMarkers`; ctor writes the first
  `SetupStartMarker`; `ClearQueues()` = fence → clear → base → new `SetupStartMarker`;
  markers never visible to `RepositoryEvents` assertions.
- [x] **#225 CatchUpConnection** (Foundation) — `GetQueuedListener` untracked by design
  (XML-doc why); `lastDelivered` set *after* `base.GotEvent`; bounded `IsLive` wait first;
  re-read stream ends via `ReadStreamBackward` every pass; timeout names laggards and busy
  queues.
- [x] **#226 FaultInjectingConfiguredConnection** + `InjectedSaveException` — only repository
  `Save` faults; `GetCorrelatedRepository` composes over the faulting repo; reads, readers,
  listeners pass through.
- [x] **#228 ProjectedEvent tagging** — `SubscribeToAll`/`SubscribeToAllFrom` only:
  `ProjectedStream = Link.EventStreamId`, `OriginalEventNumber = Event.EventNumber`,
  `EventNumber = Link.EventNumber`; stream subscriptions and batch reads unchanged; keep the
  `evt.Event != null` null-linkto guard; verify via mapping-parity test against the mock (no
  live-ESDB test project exists). **Release-notes callout.**
- [x] **#229 IsLive XML docs** — document the contract as amended in § 0.15.2/7 (Task
  completes at listener *start*, not live; a "live" model can be empty/stale; point to
  `WaitForCatchUp`); `IsLiveObservable` is consumer-side, not an RD member — reconcile the
  issue wording, don't invent the member.
- [x] **Milestone** — create the 0.15.2 milestone and assign #223–#229.

## Why async delivery forces harness changes

The mock delivers synchronously through its internal `SingleThreadedBus`: when `Save`
returns, every subscriber has already handled the event. All of RD.Testing's assertion
contracts (`AssertNext`, `AssertEmpty`, dispatcher queue inspection) implicitly depend on
that. On the real store, `RepositoryEvents` is fed by a live asynchronous `$all`
subscription — "the save returned" no longer means "the event is in the queue." Every spec
test needs an explicit delivery fence, and the harness must provide one that doesn't pollute
assertions.

---

## 0.15.2 — primitives and patterns

### 1. TestQueue: signal WaitForMsgId waiters before the type filter

Today `TestQueue.Handle` applies the constructor's `MessageTypeFilter` before enqueueing
**and** before signaling `_idWatchList` waiters, so a message outside the filter can never
complete a `WaitForMsgId` wait. Change the ordering: signal id-watchers first, then apply the
filter for enqueue.

Signal order alone is not enough: `Handle` must also record ids that arrive with **no watcher
registered** — insert an already-set wait handle into `_idWatchList` so a `WaitForMsgId` that
starts after delivery completes immediately (`WaitForMsgId` already short-circuits on a set
handle). On the synchronous mock this is the *only* working path: delivery completes inside
the marker append, before the fence's wait registers, and the filtered marker never enters
the queue for the late-arrival scan. Growth is bounded by `Clear()`, same as the queue.

This enables an **invisible fence** — a fence message completes a `WaitForMsgId` wait without
ever entering the queue, so it can't break a subsequent `AssertEmpty`. The only observable
change is that waits on filtered-out message ids now complete instead of timing out — a fix.
Everything below depends on it; land it first.

While in the file: `WaitFor`/`WaitForMultiple<T>` poll a full queue snapshot per iteration
and use `is T` at wait time versus `IsAssignableFrom` at ingest — both get hotter and more
visible under async delivery; worth tightening in the same pass.

### 2. AwaitEventDelivery: the spec-base fence

With the TestQueue change in the same release, the fence ships in its invisible form
directly:

```csharp
// Brackets a test's arrange region in $all. Plain Messages, NOT Events, so
// RepositoryEvents (filter: typeof(Event)) never captures them — but WaitForMsgId
// still signals (per change 1).
public sealed record SetupStartMarker : Message;
public sealed record DeliveryFence : Message;

// A '$'-free, '-'-free stream: no $ce category link is emitted for markers, and the
// $et link that is emitted resolves to a ProjectedEvent the connector drops.
private const string MarkerStream = "setupMarkers";

public void AwaitEventDelivery() {
    var fence = new DeliveryFence();
    WriteMarker(fence);
    RepositoryEvents.WaitForMsgId(fence.MsgId, TestTimeouts.WaitFor);
}

public override void ClearQueues() {
    AwaitEventDelivery();                // fence async $all delivery so no in-flight event lands post-clear
    base.ClearQueues();
    WriteMarker(new SetupStartMarker()); // open the next arrange region
}

private void WriteMarker(Message marker) =>
    StreamStoreConnection.AppendToStream(
        MarkerStream, ExpectedVersion.Any, credentials: null, EventSerializer.Serialize(marker));
```

Because `$all` delivers in order, the fence's arrival proves every event committed before it
is already queued. The fence covers events committed before it — synchronous handler saves —
not async cascades (process managers, timers); those need the read-model barrier below. The
spec constructor writes a `SetupStartMarker` so the first arrange region is bracketed from
the start.

**On the 0.15.2 mock this is a near-no-op** — delivery is synchronous, so the fence
completes immediately. That is the point: consumers adopt fence-then-assert against 0.15.2
at their own pace, and the calls become load-bearing when 0.16.0 flips the backend.

### 3. CatchUpConnection: the standard read-model barrier

An `IConfiguredConnection` decorator providing a deterministic "all read models have
consumed everything committed to their streams" barrier. New public class in Foundation —
it is useful in production seeding/export paths, not just tests, and works against the mock
and the ESDB wrapper today.

```csharp
public sealed class CatchUpConnection(IConfiguredConnection inner) : IConfiguredConnection {
    // Delegates everything; GetListener returns a TrackedStreamListener and records it.
    // GetQueuedListener is deliberately NOT tracked: QueuedStreamListener's queue buffers
    // post-receipt, so tracking it would over-report delivery.
    public void WaitForCatchUp(TimeSpan timeout, params ReadModelBase[] readModels);
}
```

`TrackedStreamListener` (private, extends `StreamListener`) exposes
`DeliveredThrough = max(startCheckpoint, lastDelivered)`, with `lastDelivered` recorded in a
`GotEvent` override **after** the base publishes into the subscriber's queue. So
`DeliveredThrough >= N` means "event N is in the read model's queue or applied," never "in
flight." This fixes two ambiguities in the base `StreamListener.Position`: it advances at
receipt (before the subscriber sees the event) and initializes to 0 (indistinguishable from
"applied event 0").

Barrier mechanics:

1. Bound the `IsLive` wait first — `Task.WhenAll(readModels.Select(rm => rm.IsLive))` with
   the timeout. An unbounded wait here has hung CI for hours.
2. Loop until deadline, checking in causal order each pass: every tracked listener's
   `DeliveredThrough` against its stream's end (re-read via `ReadStreamBackward` every pass,
   so the target moves until the store is quiet), then every read model's
   `Idle && MessageCount == 0`.
3. On timeout, throw naming each laggard (`"{stream} delivered {n} of {target}"`) and each
   busy queue (`"queue {RmType} (count {n})"`), so the failure says *what* is behind, not
   just that something is.

This replaces every heuristic wait (count-stability windows, version guessing, bare
`IsLive`) whose failure mode is false completion under scheduler lag.

### 4. FaultInjectingConfiguredConnection

A save-fault injection decorator for RD.Testing; purely additive:

```csharp
// IConfiguredConnection decorator: wraps the repositories so Save throws
// InjectedSaveException when a Func<IEventSource, bool> predicate matches;
// reads and listeners pass through untouched.
public sealed class FaultInjectingConfiguredConnection(IConfiguredConnection inner, Func<IEventSource, bool> shouldFail)
```

Deterministically exercises save-failure and compensation paths; RD.Testing has no
equivalent today.

### 5. CI-aware timeouts

A single timeout source in RD.Testing, keyed on `GITHUB_ACTIONS`:

| Purpose | Local | CI |
|---|---|---|
| `WaitFor` (TestQueue / RepositoryEvents waits) | 500 ms | 5 s |
| `CommandTimeout` (Send response waits) | 500 ms | 10 s |
| `ThrottleWaitFor` (real-time Rx operators) | 2 s | 10 s |

Plus `MaxCpuCount=1` runsettings guidance (sequential assembly execution; concurrent
in-process stores starve the thread pool) and the local repro recipe for 2-core flakes:
launch the test runner with `start /affinity` (2 cores) and `GITHUB_ACTIONS=true`.

### 6. ProjectedEvent tagging in EventStoreConnectionWrapper — the one behavior change

Link-resolved deliveries from the production ESDB wrapper start arriving as `ProjectedEvent`
instead of plain `RecordedEvent`, matching the mock's tagging of its emulated projection
copies (field mapping in [streamstore-storage-port.md](streamstore-storage-port.md)).

Since `ProjectedEvent : RecordedEvent`, consumers that don't type-check are unaffected;
consumers with `is ProjectedEvent` dedup guards (the harness connector) start working
correctly against real ES for the first time. Landing it here makes the connection contract
uniform across mock and real wrapper before the port, so the 0.16.0 wrapper conforms to an
already-published contract. Deserves the release-notes callout.

### 7. IsLive: document the contract

The `IsLive` **Task** completes when each `StartAsync` read task has read history and merely
*started* its listener (`blockUntilLive: false`) — before catch-up completes, and before any
downstream batching cache flushes (see 0.16.0 § 5). Consumers waiting on the Task can observe
read models that are "live" but empty or stale. (`IsLiveObservable` is a consumer-side
construct, not an RD member; post-flush signals fire after the Task.) Document this in
0.15.2; behavioral alignment changes consumer-visible timing and belongs in 0.16.0 (#234).

### Consumer guidance for the 0.15.2 window

Migrate test patterns now, while the backend is still synchronous and every change is
verifiable as a no-op:

- assert immediately after save → fence first (`AwaitEventDelivery()`)
- `AssertEmpty` after seeding → the fenced `ClearQueues()` handles it
- read-model state checks after command dispatch → `CatchUpConnection.WaitForCatchUp(...)`
- raw waits/sleeps → `WaitForMsgId` or bounded `AssertEx.IsOrBecomesTrue` with CI-aware
  timeouts

RD's own `MockRepositorySpecification`-derived suite migrates in this window too — it is the
first consumer.

---

## 0.16.0 — the backend flip

### 1. The gRPC wrapper

Per [streamstore-storage-port.md](streamstore-storage-port.md). ProjectedEvent tagging and
null-linkto handling are already contract-uniform from 0.15.2; the wrapper conforms.

### 2. Spec base backend swap

`MockRepositorySpecification` keeps its API (Dispatcher, `RepositoryEvents`, `Repository`,
listeners) and swaps the backing store underneath: each test gets a fresh partition (its own
SQLite database) on a shared in-process TestServer host — hermetic, parallel-safe, no port
contention (host composition in the storage-port doc).

### 3. Residual test migration

Tests migrated during the 0.15.2 window keep passing. What surfaces now is the genuinely
racy remainder — tests whose assumptions no fence can paper over (ordering across
independent subscriptions, reliance on synchronous cascade timing). Fix these individually;
the failure diagnostics from the barrier and fences name what's behind.

Downstream consumers inherit the same sequence when they upgrade; the 0.15.2 consumer
guidance doubles as their upgrade guide.

### 4. Deletions

Mock-only artifacts die with the mock — delete compensations rather than migrate them:

- The catch-up→live switchover gap in the mock's `SubscribeToStreamFrom` (`#mock-life`) and
  anything compensating for it.
- `resolveLinkTos` accepted-but-ignored; the real store and wrapper handle link resolution.

### 5. IsLive: complete at the live transition, fault on drop, align with observable

Three fixes, all consumer-visible timing changes (#234):

- **Upstream edge**: each `StartAsync` read task calls `listener.Start(...,
  blockUntilLive: false, ...)` and completes when the subscription is merely *started* —
  every event between `reader.Position` and the live transition is still in flight when
  `IsLive` completes. The read task must complete on the listener's live signal instead.
  (The read→listen handoff itself is lossless — the listener catch-up-subscribes from
  `reader.Position`, so transition-window events are replayed, never skipped. The defect is
  the signal, not the handoff.)
- **Fault on drop**: `StreamListener` sets its live gate on subscription drop and on
  Dispose, so a dead listener reads as live and can pass liveness gates. A drop must fault
  the read task; `blockUntilLive` waiters unblock via the fault, not a fake live.
- **Downstream edge**: the Task vs `IsLiveObservable` divergence documented in 0.15.2 —
  align so awaiting `IsLive` guarantees a queryable read model, or keep the two signals as
  explicitly distinct contracts.

Fixing the signal at the source shrinks every downstream compensation: gating on the
observable instead of the Task, barrier-wrapping read models because "IsLive alone
under-waits," and the barrier's known blindness to dropped subscriptions.

### 6. Packaging

The host dependency decision (fold `ReactiveDomain.StreamStore`, `.Sqlite`, and
`Microsoft.AspNetCore.TestHost` into `ReactiveDomain.Testing` vs a separate
`ReactiveDomain.Testing.Host` package) — see the storage-port doc.
