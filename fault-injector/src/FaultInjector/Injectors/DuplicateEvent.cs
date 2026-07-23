using Dapper;
using FaultInjector.GroundTruth;
using Npgsql;

namespace FaultInjector.Injectors;

public class DuplicateEvent(string eventsConnectionString, FaultLog log)
{
    private readonly string _conn = eventsConnectionString;
    private readonly FaultLog _log = log;

    public async Task InjectAsync(string? reference = null, string? eventType = null, string? scenario = null)
    {
        await using var c = new NpgsqlConnection(_conn);

        if (reference is null)
        {
            var t = await c.QuerySingleOrDefaultAsync<(string? Reference, string? Type)>(
                Queries.EventQueries.GetRandomTransactionEvent);
            if (t.Reference is null) { Console.WriteLine("No eligible event to duplicate."); return; }
            reference = t.Reference; eventType = t.Type;
        }

        var row = await c.QuerySingleOrDefaultAsync<DuplicateRow>(
            Queries.EventQueries.GetEventForDuplication, new { reference, eventType });
        if (row is null) { Console.WriteLine($"No {eventType} event for {reference}."); return; }

        var newId = Guid.NewGuid();
        var newSeq = await c.ExecuteScalarAsync<long>(Queries.EventQueries.GetNextSeqId);
        var newVersion = await c.ExecuteScalarAsync<long>(
            Queries.EventQueries.GetNextVersionForStream, new { streamId = row.StreamId });

        await c.ExecuteAsync(Queries.EventQueries.InsertEvent, new
        {
            SeqId = newSeq,
            Id = newId,
            row.StreamId,
            Version = newVersion,
            row.Data,
            row.Type,
            row.Timestamp,
            row.TenantId,
            row.DotNetType,
            row.IsArchived
        });

        // Allow overriding the scenario via environment variable for testing batch faults
        var envScenario = Environment.GetEnvironmentVariable("SCENARIO");
        if (!string.IsNullOrEmpty(envScenario))
        {
            scenario = envScenario;
        }

        await _log.RecordAsync(new InjectedFault(
            Guid.NewGuid(), "DuplicateEvent", "MigrationFault", scenario ?? "single_duplicate",
            reference, row.StreamId, $"{eventType} in stream {row.StreamId}",
            null, newId.ToString(), DateTime.UtcNow, false));

        Console.WriteLine($"INJECTED duplicate: {eventType} for {reference} (new id {newId}).");
    }
    public async Task RevertAsync(InjectedFault fault)
    {
        await using var c = new NpgsqlConnection(_conn);
        await c.ExecuteAsync(Queries.EventQueries.DeleteEventById,
            new { id = fault.InjectedValue });
        await _log.MarkRevertedAsync(fault.Id);
        Console.WriteLine($"REVERTED duplicate: removed {fault.InjectedValue}.");
    }

    private class DuplicateRow
    {
        public string StreamId { get; set; } = "";
        public long Version { get; set; }
        public string Data { get; set; } = "";
        public string Type { get; set; } = "";
        public DateTime Timestamp { get; set; }
        public string? TenantId { get; set; }
        public string? DotNetType { get; set; }
        public bool IsArchived { get; set; }
    }
}