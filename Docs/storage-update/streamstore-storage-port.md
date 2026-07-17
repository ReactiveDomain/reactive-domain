# StreamStore Storage Port

Ship a concrete gRPC-backed `IStreamStoreConnection` over the StreamStore SQLite engine. It
becomes both a production backend and the backing store for `ReactiveDomain.Testing`
(replacing `MockStreamStoreConnection`). Targets **0.16.0**; the test-infrastructure
primitives it depends on ship ahead of it in 0.15.2 — see
[test-infrastructure-plan.md](test-infrastructure-plan.md) for the release split.

Tracked in #230 (wrapper) and #235 (packaging); the ProjectedEvent tagging contract it relies
on lands first in #228. Relates to #217 (shed transitive EventStore.ClientAPI).

## Current state

- `IStreamStoreConnection` lives in `ReactiveDomain.Persistence`
  (`src/ReactiveDomain.Persistence/IStreamStoreConnection.cs`). Two implementations:
  - `EventStoreConnectionWrapper` — production, wraps the legacy TCP client
    (`EventStore.Client 22.0.0`, `EventStore.ClientAPI`), sync-over-async throughout. Its
    `ConnectionHelpers` extension class is the ES⇄RD mapping seam a new backend re-implements.
  - `MockStreamStoreConnection` — `ReactiveDomain.Testing`, in-memory, synchronous delivery.
- The Persistence assembly ships inside the umbrella `ReactiveDomain` package, which
  hard-depends on `EventStore.Client 22.0.0`. `IStreamStoreConnection.cs` carries a leftover
  `using EventStore.ClientAPI;` even though the interface uses only RD types.

## What StreamStore provides

- **`ReactiveDomain.StreamStore`** — the `MultiDbStreams` gRPC service, the client-side
  `PartitionInterceptor`, and the `AddStreamStoreSqliteMultiDb(dataDir, maxEventSize)` DI
  extension.
- **`ReactiveDomain.StreamStore.Sqlite`** — the SQLite multi-database engine.
- The host speaks the KurrentDB/ESDB gRPC wire protocol, so the client is stock
  **`KurrentDB.Client`** (currently 1.4.0). One wrapper therefore serves StreamStore.Host,
  KurrentDB, and ESDB — the gRPC protocol is theirs; StreamStore adopted it.

## The connection wrapper

New class in `ReactiveDomain.Persistence` implementing `IStreamStoreConnection` over
`KurrentDBClient`. StreamStore has a conformance-tested implementation
(`ESDBConnectionWrapper`); adopt it rather than writing fresh.

Construction:

- `KurrentDBClientSettings` from an `esdb://…?tls=false` URI. `DefaultDeadline` of 2 minutes
  (headroom for large multi-event transactions).
- Interceptor list on the settings: `PartitionInterceptor(partition)` when a partition is set,
  a bearer-token interceptor when a token is set. Connection-scoped headers must ride client
  interceptors — per-call `UserCredentials` map to gRPC call credentials, which .NET drops on
  insecure (tls=false) channels.
- `settings.CreateHttpMessageHandler` accepts an optional handler factory — the seam that lets
  tests point the client at an in-process ASP.NET TestServer with no socket.

Behavior:

- **ProjectedEvent tagging is required, not optional.** A delivery with `ResolvedEvent.Link`
  non-null is a projection copy (`$ce-`/`$et-`/`$streams` link), not a distinct fact. Return:

  ```csharp
  new ProjectedEvent(
      link.EventStreamId,                          // ProjectedStream: the $ce-/$et- stream this copy lives in
      e.EventNumber.ToInt64(),                     // OriginalEventNumber: position in the source stream
      e.EventStreamId,                             // source stream
      e.EventId.ToGuid(),
      resolvedEvent.OriginalEventNumber.ToInt64(), // EventNumber: position in the projected stream
      e.EventType, e.Data.ToArray(), e.Metadata.ToArray(),
      isJson, e.Created, e.Created.Ticks);
  ```

  This matches `MockStreamStoreConnection`'s tagging of its emulated projection copies.
  `MockRepositorySpecification`'s connector bus dedups on `is ProjectedEvent`; without the tag,
  every domain event is delivered multiple times through its link copies.
  `EventStoreConnectionWrapper` adopts the same tagging in 0.15.2, so by the time this
  wrapper ships the tagging is an established contract across all implementations, not a new
  behavior.
- Non-link deliveries map to plain `RecordedEvent` using `OriginalEventNumber` — position in
  the stream being read, matching `EventStoreConnectionWrapper`.
- Carry forward the null-resolved-event guards specified in
  [../null-linkto-handling.md](../null-linkto-handling.md) (skip null link targets in
  subscriptions and batch reads; checkpoints track link positions, so gaps are safe).
- **Carry the source position(s) through the delivery envelope** — the envelope half of
  #211. The wrapper defines the `RecordedEvent` mapping; deferring position carry means
  touching the same mapping twice when read-model checkpointing (`CheckpointPosition`,
  `WaitForPosition`) lands. On a single-store total order the `$all` position is a scalar
  watermark.

## Partition model

- Routing header: **`streamstore-partition`**, stamped per-connection by
  `PartitionInterceptor`. One SQLite database per partition value, provisioned on first use.
  Partition ids match `^[a-z][a-z0-9_]*$`.
- The test harness derives a fresh partition per test (e.g. `b_` + `Guid.NewGuid():N`), so
  thousands of tests share one host with hard isolation and no port contention.

## Hosting shapes

Both shapes are the same composition:
`CreateSlimBuilder` → `AddGrpc()` → `AddStreamStoreSqliteMultiDb(dataDir, maxEventSize)` →
`MapGrpcService<MultiDbStreams>()` (resolve the service eagerly so the engine boots at start).

- **In-process TestServer** — the `ReactiveDomain.Testing` default. `UseTestServer()`, backing
  data in a temp subdirectory, and the TestServer's `CreateHandler()` fed into the wrapper's
  handler factory. No sockets; hermetic; parallel-safe.
- **Loopback sidecar** — integration and production. Kestrel plaintext HTTP/2 on
  `127.0.0.1`, port 0 to pick a free port. On Windows, probe the bound port for the
  WinNAT/Hyper-V excluded-port-range failure (`WSAEACCES` on connect despite a successful
  bind) and rebind — retry ~10 ports. Endpoint: `esdb://127.0.0.1:{port}?tls=false`.

## Packaging

- The wrapper goes in `ReactiveDomain.Persistence`; the umbrella `ReactiveDomain` package
  gains a `KurrentDB.Client` dependency.
- The test host needs `ReactiveDomain.StreamStore`, `ReactiveDomain.StreamStore.Sqlite`, and
  `Microsoft.AspNetCore.TestHost`. Decide whether `ReactiveDomain.Testing` absorbs those
  dependencies or a separate `ReactiveDomain.Testing.Host` package carries them (keeps the
  base testing package light for consumers that only want the bus/spec doubles).
- End state: `EventStoreConnectionWrapper` and the `EventStore.Client 22.0.0` TCP dependency
  retire once consumers are on the gRPC wrapper; the stray `using EventStore.ClientAPI;` in
  `IStreamStoreConnection.cs` goes with them. The wrapper becomes the contract.

## Verification

StreamStore's conformance suite already covers the wrapper against its host. Once the test
infrastructure plan lands, RD's own spec suite runs against the real store continuously, which
keeps the wrapper honest from the consumer side.

One case the conformance suite must include explicitly: **writes racing the catch-up→live
transition.** The gRPC protocol makes the transition server-side (history, caught-up signal,
live — one stream), so there is no client-side switchover to race; the case pins the
StreamStore host's implementation of that boundary. Events committed during the transition are
delivered exactly once, and the live signal fires only after the transition completes —
`ReadModelBase.IsLive` (#234) is only as honest as this signal.
