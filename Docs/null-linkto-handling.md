# Null Link-To Event Handling

## Background

EventStore uses **link events** (`$>`) in category streams (`$ce-`), event-type streams (`$et-`),
and user-defined projections to reference events in other streams. When a linked-to event is
**deleted or scavenged**, the link event remains at its original position in the stream, but
resolving it returns `null` for the `Event` property on `ResolvedEvent`.

This is by design in EventStore: **stream positions are immutable**. Deleting or scavenging the
target of a link does not renumber the stream. Any downstream checkpoints that track "last
processed event number" remain valid — they simply encounter a gap where the deleted event was.

## What Changed

### EventStoreConnectionWrapper

All subscription and batch-read paths now guard against null resolved events:

- **Subscription callbacks** (`SubscribeToStream`, `SubscribeToStreamFrom`, `SubscribeToAll`,
  `SubscribeToAllFrom`): Each callback checks `evt.Event != null` before invoking `eventAppeared`.
  When `Event` is null, the subscription silently skips the entry. The `OriginalEvent` (i.e. the
  link itself) is still present with its stream position, so checkpoint tracking is unaffected.

- **`ToRecordedEvents`** (batch reads via `ReadStreamForward` / `ReadStreamBackward`): Converts
  `ES.ResolvedEvent[]` to `RecordedEvent[]`, skipping entries where `Event` is null. The resulting
  array may be shorter than the input slice. This is correct — the `StreamEventsSlice` metadata
  (`NextEventNumber`, `LastEventNumber`, `IsEndOfStream`) is set by EventStore based on stream
  positions, not on the count of non-null events.

### JsonMessageSerializer

`Deserialize` returns `null` when `Metadata` or `Data` is null or zero-length, instead of
throwing during `JObject.Parse`. This handles system events and edge cases where event payloads
are absent.

## Why This Is Correct

### Stream positions are immutable

When EventStore scavenges or soft-deletes an event:
1. The **link event** in the category/projection stream keeps its position.
2. The **resolved event** returns `Event = null`, `Link = <the link>`.
3. `OriginalEvent` (which returns `Link ?? Event`) still has the correct `EventNumber`.
4. `NextEventNumber` on the slice is based on the link positions, not the resolved targets.

Downstream consumers tracking checkpoints (e.g. `lastCheckpoint` in `SubscribeToStreamFrom`)
are unaffected because the checkpoint is the position of the **link** in the stream being read,
not the position of the target event in its original stream.

### Aggregate streams are unaffected

`StreamStoreRepository` reads aggregate streams directly (not category projections). Aggregate
streams contain the events themselves, not links. Deletion or scavenging of individual events
within an aggregate stream is not a supported operation in normal usage — if it happens, it
represents data corruption and should surface as an error rather than be silently skipped.

For this reason, `StreamStoreRepository` does **not** filter null results from `Deserialize`.
If `Deserialize` returns null for an event in an aggregate stream, `EventRouter.Route(null)`
will throw `ArgumentNullException`, which is the correct behaviour — it signals corruption
rather than masking it.

### Deserialize null return is intentional

The `Deserialize` null guard is primarily relevant for:
- **Subscription-based consumers** (e.g. read-model projectors on `$ce-` or `$all`) that may
  encounter system events (`$metadata`, `$settings`) with no user-defined metadata/data.
- **Events from deleted streams** that still have a `RecordedEvent` shell but empty payloads.

These consumers are expected to handle null deserialization results — typically by skipping
the event, which aligns with the subscription-level null guard in `EventStoreConnectionWrapper`.
