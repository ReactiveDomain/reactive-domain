using System.Net;
using System.Text;
using Newtonsoft.Json;
using ES = EventStore.ClientAPI;

namespace NullLinkToSpike;

class Program
{
    // ── Event variants ──────────────────────────────────────────────────
    // 1. Normal: data + metadata
    // 2. Marker: minimal JSON object (like an Event with no extra properties)
    // 3. Empty metadata: valid data, zero-length metadata
    // 4. Empty data: zero-length data, valid metadata

    static readonly byte[] NormalData = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(
        new { MsgId = Guid.NewGuid(), Foo = "bar", Version = 1, CorrelationId = Guid.NewGuid(), CausationId = Guid.NewGuid() }));
    static readonly byte[] NormalMetadata = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(
        new { EventClrQualifiedTypeName = "NullLinkToSpike.NormalEvent, NullLinkToSpike" }));

    static readonly byte[] MarkerData = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(
        new { MsgId = Guid.NewGuid(), Version = 1, CorrelationId = Guid.NewGuid(), CausationId = Guid.NewGuid() }));
    static readonly byte[] MarkerMetadata = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(
        new { EventClrQualifiedTypeName = "NullLinkToSpike.MarkerEvent, NullLinkToSpike" }));

    static readonly byte[] ValidData = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new { MsgId = Guid.NewGuid(), Value = 42 }));
    static readonly byte[] ValidMetadata = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(
        new { EventClrQualifiedTypeName = "NullLinkToSpike.TestEvent, NullLinkToSpike" }));

    static ES.EventData[] CreateEventVariants()
    {
        return new[]
        {
            new ES.EventData(Guid.NewGuid(), "NormalEvent",    true, NormalData,       NormalMetadata),
            new ES.EventData(Guid.NewGuid(), "MarkerEvent",    true, MarkerData,       MarkerMetadata),
            new ES.EventData(Guid.NewGuid(), "EmptyMdEvent",   true, ValidData,        Array.Empty<byte>()),
            new ES.EventData(Guid.NewGuid(), "EmptyDataEvent", true, Array.Empty<byte>(), ValidMetadata),
        };
    }

    static async Task Main()
    {
        var endpoint = new IPEndPoint(IPAddress.Loopback, 1113);
        var settings = ES.ConnectionSettings.Create()
            .DisableTls()
            .DisableServerCertificateValidation()
            .KeepReconnecting()
            .Build();

        using var conn = ES.EventStoreConnection.Create(settings, endpoint, "NullLinkToSpike");
        await conn.ConnectAsync();
        Console.WriteLine("Connected to ESDB on tcp://127.0.0.1:1113\n");

        await Scenario1_Baseline(conn);
        await Scenario2_SoftDelete(conn);
        await Scenario3_HardDeleteAndScavenge(conn);
        await Scenario4_SystemEvents(conn);

        Console.WriteLine("\n=== ALL SCENARIOS COMPLETE ===");
    }

    // ════════════════════════════════════════════════════════════════════
    // Scenario 1: Baseline — write, read directly, read via $ce-
    // ════════════════════════════════════════════════════════════════════
    static async Task Scenario1_Baseline(ES.IEventStoreConnection conn)
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════╗");
        Console.WriteLine("║  SCENARIO 1: BASELINE (no delete)                   ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════╝\n");

        var stream = $"spike-{Guid.NewGuid():N}";
        var events = CreateEventVariants();
        await conn.AppendToStreamAsync(stream, ES.ExpectedVersion.NoStream, events);
        Console.WriteLine($"Wrote {events.Length} events to '{stream}'");

        // Read directly
        Console.WriteLine("\n── Direct read ──");
        var slice = await conn.ReadStreamEventsForwardAsync(stream, 0, 100, true);
        PrintSlice(slice);

        // Read via $ce- (may need a moment for projections to catch up)
        await Task.Delay(2000);
        Console.WriteLine("\n── Read via $ce-spike (resolve links) ──");
        var ceSlice = await conn.ReadStreamEventsForwardAsync("$ce-spike", 0, 100, true);
        PrintSlice(ceSlice);

        Console.WriteLine();
    }

    // ════════════════════════════════════════════════════════════════════
    // Scenario 2: Soft-delete — write, soft-delete, read via $ce-
    // ════════════════════════════════════════════════════════════════════
    static async Task Scenario2_SoftDelete(ES.IEventStoreConnection conn)
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════╗");
        Console.WriteLine("║  SCENARIO 2: SOFT DELETE                            ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════╝\n");

        var stream = $"spike-{Guid.NewGuid():N}";
        var events = CreateEventVariants();
        await conn.AppendToStreamAsync(stream, ES.ExpectedVersion.NoStream, events);
        Console.WriteLine($"Wrote {events.Length} events to '{stream}'");

        // Wait for projections
        await Task.Delay(2000);

        Console.WriteLine("\n── $ce-spike BEFORE soft-delete ──");
        var ceBefore = await conn.ReadStreamEventsForwardAsync("$ce-spike", 0, 1000, true);
        PrintSliceForStream(ceBefore, stream);

        // Soft delete
        await conn.DeleteStreamAsync(stream, ES.ExpectedVersion.Any, hardDelete: false);
        Console.WriteLine($"\nSoft-deleted '{stream}'");

        Console.WriteLine("\n── Direct read after soft-delete ──");
        var directSlice = await conn.ReadStreamEventsForwardAsync(stream, 0, 100, true);
        Console.WriteLine($"  Status: {directSlice.Status}");
        if (directSlice.Status == ES.SliceReadStatus.Success)
            PrintSlice(directSlice);

        Console.WriteLine("\n── $ce-spike AFTER soft-delete (resolve links) ──");
        var ceAfter = await conn.ReadStreamEventsForwardAsync("$ce-spike", 0, 1000, true);
        PrintSliceForStream(ceAfter, stream);

        Console.WriteLine();
    }

    // ════════════════════════════════════════════════════════════════════
    // Scenario 3: Hard-delete + scavenge — write, hard-delete, scavenge, read via $ce-
    // ════════════════════════════════════════════════════════════════════
    static async Task Scenario3_HardDeleteAndScavenge(ES.IEventStoreConnection conn)
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════╗");
        Console.WriteLine("║  SCENARIO 3: HARD DELETE + SCAVENGE                 ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════╝\n");

        var stream = $"spike-{Guid.NewGuid():N}";
        var events = CreateEventVariants();
        await conn.AppendToStreamAsync(stream, ES.ExpectedVersion.NoStream, events);
        Console.WriteLine($"Wrote {events.Length} events to '{stream}'");

        // Wait for projections
        await Task.Delay(2000);

        Console.WriteLine("\n── $ce-spike BEFORE hard-delete ──");
        var ceBefore = await conn.ReadStreamEventsForwardAsync("$ce-spike", 0, 1000, true);
        PrintSliceForStream(ceBefore, stream);

        // Hard delete
        await conn.DeleteStreamAsync(stream, ES.ExpectedVersion.Any, hardDelete: true);
        Console.WriteLine($"\nHard-deleted '{stream}'");

        Console.WriteLine("\n── $ce-spike AFTER hard-delete, BEFORE scavenge ──");
        var ceAfterDelete = await conn.ReadStreamEventsForwardAsync("$ce-spike", 0, 1000, true);
        PrintSliceForStream(ceAfterDelete, stream);

        // Trigger scavenge
        Console.WriteLine("\nTriggering scavenge...");
        using var http = new HttpClient();
        var resp = await http.PostAsync("http://127.0.0.1:2113/admin/scavenge", null);
        Console.WriteLine($"Scavenge response: {resp.StatusCode}");
        var scavengeBody = await resp.Content.ReadAsStringAsync();
        Console.WriteLine($"Scavenge body: {scavengeBody}");

        // Poll for scavenge completion (check $scavenges stream)
        Console.WriteLine("Waiting for scavenge to complete...");
        for (int i = 0; i < 60; i++)
        {
            await Task.Delay(2000);
            try
            {
                var scavengeSlice = await conn.ReadStreamEventsBackwardAsync("$scavenges", -1, 1, true);
                if (scavengeSlice.Status == ES.SliceReadStatus.Success && scavengeSlice.Events.Length > 0)
                {
                    var lastEvent = scavengeSlice.Events[0];
                    var eventData = Encoding.UTF8.GetString(lastEvent.Event.Data);
                    Console.WriteLine($"  Last scavenge event: {lastEvent.Event.EventType}");
                    Console.WriteLine($"  Data: {eventData}");
                    if (lastEvent.Event.EventType.Contains("Completed", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine("  Scavenge completed!");
                        break;
                    }
                }
            }
            catch
            {
                // $scavenges might not exist yet
            }
            Console.Write(".");
        }

        Console.WriteLine("\n── $ce-spike AFTER scavenge (resolve links) ──");
        var ceAfterScavenge = await conn.ReadStreamEventsForwardAsync("$ce-spike", 0, 1000, true);
        PrintSliceForStream(ceAfterScavenge, stream);

        Console.WriteLine();
    }

    // ════════════════════════════════════════════════════════════════════
    // Scenario 4: System events from $all
    // ════════════════════════════════════════════════════════════════════
    static async Task Scenario4_SystemEvents(ES.IEventStoreConnection conn)
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════╗");
        Console.WriteLine("║  SCENARIO 4: SYSTEM EVENTS FROM $all               ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════╝\n");

        var slice = await conn.ReadAllEventsForwardAsync(ES.Position.Start, 50, true);
        int count = 0;
        foreach (var evt in slice.Events)
        {
            // Show system events (starting with $) and a few user events
            if (evt.Event.EventType.StartsWith("$") || count < 5)
            {
                PrintResolvedEvent(evt, "  ");
                count++;
            }
            if (count >= 20) break;
        }

        Console.WriteLine();
    }

    // ── Printing helpers ────────────────────────────────────────────────

    static void PrintSlice(ES.StreamEventsSlice slice)
    {
        Console.WriteLine($"  Status: {slice.Status}, Events: {slice.Events.Length}");
        foreach (var evt in slice.Events)
        {
            PrintResolvedEvent(evt, "  ");
        }
    }

    static void PrintSliceForStream(ES.StreamEventsSlice slice, string filterStream)
    {
        if (slice.Status != ES.SliceReadStatus.Success)
        {
            Console.WriteLine($"  Status: {slice.Status}");
            return;
        }

        // Filter to only events from our stream
        var matching = slice.Events.Where(e =>
        {
            // For resolved link events, check if the link points to our stream
            if (e.Link != null)
            {
                var linkData = Encoding.UTF8.GetString(e.Link.Data);
                return linkData.Contains(filterStream);
            }
            // For non-link events, check OriginalStreamId
            return e.OriginalStreamId == filterStream;
        }).ToArray();

        Console.WriteLine($"  Total in $ce-spike: {slice.Events.Length}, matching '{filterStream}': {matching.Length}");
        foreach (var evt in matching)
        {
            PrintResolvedEvent(evt, "  ");
        }
    }

    static void PrintResolvedEvent(ES.ResolvedEvent resolved, string indent)
    {
        Console.WriteLine($"{indent}┌─ ResolvedEvent ─────────────────────────────");
        Console.WriteLine($"{indent}│ OriginalStreamId: {resolved.OriginalStreamId}");
        Console.WriteLine($"{indent}│ OriginalEventNumber: {resolved.OriginalEventNumber}");
        Console.WriteLine($"{indent}│ OriginalPosition: {resolved.OriginalPosition}");
        Console.WriteLine($"{indent}│ IsResolved: {resolved.IsResolved}");

        if (resolved.Event != null)
        {
            var e = resolved.Event;
            Console.WriteLine($"{indent}│ Event:");
            Console.WriteLine($"{indent}│   Stream: {e.EventStreamId}");
            Console.WriteLine($"{indent}│   Type: {e.EventType}");
            Console.WriteLine($"{indent}│   EventNumber: {e.EventNumber}");
            Console.WriteLine($"{indent}│   IsJson: {e.IsJson}");
            Console.WriteLine($"{indent}│   Data.Length: {e.Data?.Length ?? -1}");
            Console.WriteLine($"{indent}│   Metadata.Length: {e.Metadata?.Length ?? -1}");
            if (e.Data != null && e.Data.Length > 0 && e.Data.Length < 500)
                Console.WriteLine($"{indent}│   Data: {Encoding.UTF8.GetString(e.Data)}");
            if (e.Metadata != null && e.Metadata.Length > 0 && e.Metadata.Length < 500)
                Console.WriteLine($"{indent}│   Metadata: {Encoding.UTF8.GetString(e.Metadata)}");
        }
        else
        {
            Console.WriteLine($"{indent}│ Event: *** NULL ***");
        }

        if (resolved.Link != null)
        {
            var l = resolved.Link;
            Console.WriteLine($"{indent}│ Link:");
            Console.WriteLine($"{indent}│   Stream: {l.EventStreamId}");
            Console.WriteLine($"{indent}│   Type: {l.EventType}");
            Console.WriteLine($"{indent}│   EventNumber: {l.EventNumber}");
            Console.WriteLine($"{indent}│   Data.Length: {l.Data?.Length ?? -1}");
            Console.WriteLine($"{indent}│   Metadata.Length: {l.Metadata?.Length ?? -1}");
            if (l.Data != null && l.Data.Length > 0 && l.Data.Length < 500)
                Console.WriteLine($"{indent}│   Data: {Encoding.UTF8.GetString(l.Data)}");
        }
        else
        {
            Console.WriteLine($"{indent}│ Link: null");
        }

        Console.WriteLine($"{indent}└──────────────────────────────────────────────");
    }
}
