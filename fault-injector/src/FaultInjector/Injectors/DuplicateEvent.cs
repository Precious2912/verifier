using Dapper;
using FaultInjector.GroundTruth;
using Npgsql;

namespace FaultInjector.Injectors;

public class DuplicateEvent(string eventsConnectionString, FaultLog log)
{
    private readonly string _conn = eventsConnectionString;
    private readonly FaultLog _log = log;

    public async Task InjectAsync(string? reference = null, string? eventType = null)
    {
        await using var c = new NpgsqlConnection(_conn);

        if (reference is null)
        {
            var t = await c.QuerySingleOrDefaultAsync<(string? Reference, string? Type)>(Queries.EventQueries.GetRandomTransactionEvent);
            if (t.Reference is null) { Console.WriteLine("No eligible event to duplicate."); return; }
            reference = t.Reference; eventType = t.Type;
        }

        var row = await c.QuerySingleOrDefaultAsync<dynamic>(Queries.EventQueries.GetEventForDuplication, new { reference, eventType });
        if (row is null) { Console.WriteLine($"No {eventType} event for {reference}."); return; }

        var newId = Guid.NewGuid();
        var newSeq = await c.ExecuteScalarAsync<long>(Queries.EventQueries.GetNextSeqId);
        var newVersion = await c.ExecuteScalarAsync<long>(Queries.EventQueries.GetNextVersionForStream,
            new { s = (string)row.stream_id });

        await c.ExecuteAsync(Queries.EventQueries.InsertEvent, new
        {
            SeqId = newSeq,
            Id = newId,
            StreamId = (string)row.stream_id,
            Version = newVersion,
            Data = (string)row.data,
            Type = (string)row.type,
            Timestamp = (DateTime)row.timestamp,
            TenantId = (string?)row.tenant_id,
            DotNetType = (string?)row.mt_dotnet_type,
            IsArchived = (bool)row.is_archived
        });

        await _log.RecordAsync(new InjectedFault(
            Guid.NewGuid(), "DuplicateEvent", "MigrationFault",
            reference, (string)row.stream_id, $"{eventType} in stream {row.stream_id}",
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
}