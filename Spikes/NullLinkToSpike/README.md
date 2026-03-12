# Null Link-To Event Spike

## Purpose

This spike was created to empirically answer two questions raised by @joshkempner in [PR #185](https://github.com/ReactiveDomain/reactive-domain/pull/185) regarding the `Deserialize` null guard in `JsonMessageSerializer`:

```csharp
if (@event.Metadata == null || @event.Metadata.Length == 0
    || @event.Data == null || @event.Data.Length == 0)
    return null;
```

**Question 1 (Metadata):** "If the message has data but no metadata, is that always an invalid state such that we should return null?"

**Question 2 (Marker Events):** "Would a marker event (no properties other than what it inherits from Event) have empty data that would cause this to return null even though the event is valid?"

The spike also validates the broader behavior of ESDB when streams are soft-deleted, hard-deleted, and scavenged, specifically how `$ce-` category stream link-to events behave when their targets are gone.

## Prerequisites

- **EventStoreDB 23.10** running locally in insecure mode on `tcp://127.0.0.1:1113` and `http://127.0.0.1:2113`
- System projections enabled (`RunProjections: System` in the ESDB config)
- A clean or low-volume database (so projections catch up quickly to newly written events)

### Sample ESDB config (`c:\esdb\eventstore.conf`)

```yaml
---
Db: C:\ESDB\Data
Index: C:\ESDB\Index
Log: C:\ESDB\Logs
IntIp: 127.0.0.1
ExtIp: 127.0.0.1
HttpPort: 2113
IntTcpPort: 1112
ExtTcpPort: 1113
EnableExternalTcp: true
EnableAtomPubOverHTTP: true
SkipDbVerify: true
RunProjections: System
```

## How to Reproduce

1. Start ESDB 23.10 in insecure mode:
   ```
   EventStore.ClusterNode.exe --config c:\esdb\eventstore.conf --insecure
   ```

2. Run the spike:
   ```
   dotnet run --project Spikes\NullLinkToSpike
   ```

3. Output is written to the console. To capture it:
   ```powershell
   dotnet run --project Spikes\NullLinkToSpike 2>&1 | Out-File -FilePath Spikes\NullLinkToSpike\output.txt -Encoding utf8
   ```

## Test Matrix

The spike writes **4 event variants** across **3 deletion scenarios**, then reads them back both directly and via the `$ce-spike` category projection stream.

### Event Variants

| Variant | Data | Metadata |
|---|---|---|
| **NormalEvent** | `{"MsgId":"...","Foo":"bar","Version":1,"CorrelationId":"...","CausationId":"..."}` (180 bytes) | `{"EventClrQualifiedTypeName":"..."}` (76 bytes) |
| **MarkerEvent** | `{"MsgId":"...","Version":1,"CorrelationId":"...","CausationId":"..."}` (168 bytes) | `{"EventClrQualifiedTypeName":"..."}` (76 bytes) |
| **EmptyMdEvent** | Valid JSON data (59 bytes) | `byte[0]` (0 bytes) |
| **EmptyDataEvent** | `byte[0]` (0 bytes) | Valid metadata (74 bytes) |

### Scenarios

| Scenario | Description |
|---|---|
| **1. Baseline** | Write all 4 variants, read directly and via `$ce-spike` |
| **2. Soft-delete** | Write, soft-delete the stream, re-read via `$ce-spike` |
| **3. Hard-delete + scavenge** | Write, hard-delete, trigger scavenge via HTTP API, re-read via `$ce-spike` |
| **4. System events** | Read first 50 events from `$all` to observe system event data/metadata patterns |

## Findings

### 1. Marker events always have non-empty Data

A marker event with **zero additional properties** beyond the inherited `MsgId`, `Version`, `CorrelationId`, and `CausationId` serializes to **168 bytes** of JSON:

```json
{"MsgId":"73a77cbf-...","Version":1,"CorrelationId":"9a1abef5-...","CausationId":"64c29fb8-..."}
```

This will **never** be zero-length. The `Deserialize` null guard will not trigger for marker events.

### 2. ESDB preserves empty data and metadata exactly as written

- **EmptyMdEvent**: ESDB stores and returns `Data.Length: 59`, `Metadata.Length: 0`
- **EmptyDataEvent**: ESDB stores and returns `Data.Length: 0`, `Metadata.Length: 74`

ESDB does not convert zero-length byte arrays to null. The TCP client returns `byte[]` with `Length == 0`.

### 3. Soft-deleted streams produce null Event in $ce- reads

After soft-deleting a stream, reading `$ce-spike` with resolve links shows:

```
Event: *** NULL ***
Link:
  Stream: $ce-spike
  Type: $>
  Data: 0@spike-{guid}
```

The **link event** (`$>`) still exists in the `$ce-` stream, but the **resolved Event is null** because the target stream is deleted. The `$metadata` tombstone event (`{"$tb":9223372036854775807}`) also appears as a link in `$ce-`.

### 4. Hard-delete + scavenge produces the same null Event pattern

After hard-deleting and scavenging, the behavior is identical to soft-delete: `Event` is null, `Link` still present. The scavenge removes the physical data but the `$ce-` links remain as dangling references.

### 5. System events have zero-length metadata

System events like `$ProjectionsInitialized` have both `Data.Length: 0` and `Metadata.Length: 0`. Events like `$ProjectionCreated` have data (e.g., `$streams`) but `Metadata.Length: 0`. The `$metadata` events for stream ACLs have data like `{"$acl":{"$r":"$all","$mr":"$all"}}` but `Metadata.Length: 0`.

## Conclusions for PR #185

### Answer to Question 1: "Data but no metadata — always invalid?"

**For ReactiveDomain events, yes.** The `Serialize` method in `JsonMessageSerializer` always writes metadata containing at minimum the `EventClrQualifiedTypeName` or `CommonMetadata` fields. An event with data but no metadata was either:
- Written by an external system that doesn't follow the RD serialization convention
- A system event (like `$metadata`, `$ProjectionCreated`)

In both cases, `Deserialize` cannot resolve the CLR type without metadata, so returning `null` is correct. Callers like `StreamListener` and `StreamReader` already check `if (Serializer.Deserialize(recordedEvent) is IMessage @event)` and simply skip nulls.

### Answer to Question 2: "Would a marker event have empty data?"

**No.** Even the most minimal `Event` subclass with zero additional properties serializes to 168+ bytes of JSON due to inherited properties (`MsgId`, `Version`, `CorrelationId`, `CausationId`). The empty-data guard will never trigger for any event that went through `JsonMessageSerializer.Serialize`.

### The real-world null case

The null guard's primary purpose is protecting against **resolved link events in `$ce-` category streams** where the target stream has been deleted or scavenged. In this case, the `ResolvedEvent.Event` property is null (the entire event object, not just data/metadata). The existing null-link-to handling in the PR (`ToRecordedEvents()` filtering) addresses this at the correct layer — before the event ever reaches the deserializer.
