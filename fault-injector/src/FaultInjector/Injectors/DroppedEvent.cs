using Dapper;
using FaultInjector.GroundTruth;
using Npgsql;

namespace FaultInjector.Injectors;

public class DroppedEvent(string eventsConnectionString, FaultLog log)
{
    private readonly string _conn = eventsConnectionString;
    private readonly FaultLog _log = log;

    public async Task InjectAsync(string? reference = null, string? eventType = null)
    {
        await using var c = new NpgsqlConnection(_conn);

        if (reference is null)
        {
            var t = await c.QuerySingleOrDefaultAsync<(string? Reference, string? Type)>("""
                SELECT data ->> 'Reference' AS Reference, type AS Type
                FROM event_store.mt_events
                WHERE type IN ('account_debited', 'account_credited')
                ORDER BY random() LIMIT 1;
                """);
            if (t.Reference is null) { Console.WriteLine("No eligible event to drop."); return; }
            reference = t.Reference; eventType = t.Type;
        }

        const string find = """
            SELECT seq_id AS SeqId, id::text AS EventId, stream_id AS StreamId,
                   version AS Version, data::text AS Data, type AS Type,
                   timestamp AS Timestamp, tenant_id AS TenantId,
                   mt_dotnet_type AS DotNetType, is_archived AS IsArchived
            FROM event_store.mt_events
            WHERE type = @eventType AND data ->> 'Reference' = @reference
            LIMIT 1;
            """;
        var row = await c.QuerySingleOrDefaultAsync<EventRow>(find, new { reference, eventType });
        if (row is null) { Console.WriteLine($"No {eventType} event for {reference}."); return; }

        await c.ExecuteAsync("DELETE FROM event_store.mt_events WHERE id = @id::uuid;",
            new { id = row.EventId });

        await _log.RecordAsync(new InjectedFault(
            Guid.NewGuid(), "DroppedEvent", "MigrationFault",
            reference, row.StreamId, $"{eventType} in stream {row.StreamId} (seq {row.SeqId})",
            System.Text.Json.JsonSerializer.Serialize(row), null, DateTime.UtcNow, false));

        Console.WriteLine($"INJECTED drop: {eventType} for {reference} (stream {row.StreamId}).");
    }

    public async Task RevertAsync(InjectedFault fault)
    {
        var row = System.Text.Json.JsonSerializer.Deserialize<EventRow>(fault.OriginalValue!)!;
        await using var c = new NpgsqlConnection(_conn);
        await c.ExecuteAsync("""
            INSERT INTO event_store.mt_events
                (seq_id, id, stream_id, version, data, type, timestamp, tenant_id, mt_dotnet_type, is_archived)
            VALUES (@SeqId, @EventId::uuid, @StreamId, @Version, @Data::jsonb, @Type,
                    @Timestamp, @TenantId, @DotNetType, @IsArchived);
            """, row);
        await _log.MarkRevertedAsync(fault.Id);
        Console.WriteLine($"REVERTED drop: restored {row.Type} for {row.StreamId}.");
    }

    public class EventRow
    {
        public long SeqId { get; set; }
        public string EventId { get; set; } = "";
        public string? StreamId { get; set; }
        public long Version { get; set; }
        public string Data { get; set; } = "";
        public string Type { get; set; } = "";
        public DateTime Timestamp { get; set; }
        public string? TenantId { get; set; }
        public string? DotNetType { get; set; }
        public bool IsArchived { get; set; }
    }
}