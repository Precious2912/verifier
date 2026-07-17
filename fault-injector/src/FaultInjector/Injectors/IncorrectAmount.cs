using Dapper;
using FaultInjector.GroundTruth;
using Npgsql;

namespace FaultInjector.Injectors;

public class IncorrectAmount(string eventsConnectionString, FaultLog log)
{
    private readonly string _conn = eventsConnectionString;
    private readonly FaultLog _log = log;

    public async Task InjectAsync(string? reference = null, string? eventType = null, decimal? newAmount = null)
    {
        await using var c = new NpgsqlConnection(_conn);

        string origAmount;
        string eventId, streamId;

        if (reference is null)
        {
            var t = await c.QuerySingleOrDefaultAsync<(string? Reference, string? Type, string? EventId, string? StreamId, string? Amount)>(
                Queries.EventQueries.GetRandomTransactionEvent);
            if (t.Reference is null) { Console.WriteLine("No eligible event to corrupt."); return; }
            reference = t.Reference; eventType = t.Type;
            eventId = t.EventId!; streamId = t.StreamId!; origAmount = t.Amount!;
        }
        else
        {
            var t = await c.QuerySingleOrDefaultAsync<(string EventId, string StreamId, string Amount)>(Queries.EventQueries.FindEventAmountByReference, new { reference, eventType });
            if (t.EventId is null) { Console.WriteLine($"No {eventType} for {reference}."); return; }
            eventId = t.EventId; streamId = t.StreamId; origAmount = t.Amount;
        }

        var corrupted = newAmount ?? (decimal.Parse(origAmount) + 100m);

        await c.ExecuteAsync(Queries.EventQueries.UpdateEventAmount, new { id = eventId, amt = corrupted });

        await _log.RecordAsync(new InjectedFault(
            Guid.NewGuid(), "IncorrectAmount", "MigrationFault",
            reference, streamId, $"{eventType} in stream {streamId}, event {eventId}",
            origAmount, corrupted.ToString(), DateTime.UtcNow, false));

        Console.WriteLine($"INJECTED amount: {reference} {origAmount} -> {corrupted}.");
    }

    public async Task RevertAsync(InjectedFault fault)
    {
        var eventId = fault.TargetDetail!.Split("event ")[^1].Trim();
        await using var c = new NpgsqlConnection(_conn);
        await c.ExecuteAsync(Queries.EventQueries.UpdateEventAmount, new { id = eventId, amt = decimal.Parse(fault.OriginalValue!) });
        await _log.MarkRevertedAsync(fault.Id);
        Console.WriteLine($"REVERTED amount: restored {fault.OriginalValue}.");
    }
}