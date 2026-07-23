using Dapper;
using Npgsql;

namespace FaultInjector.Injectors;

public class CombinedDropDuplicate(
    string eventsConnectionString,
    DroppedEvent droppedEvent,
    DuplicateEvent duplicateEvent)
{
    private readonly string _conn = eventsConnectionString;
    private readonly DroppedEvent _drop = droppedEvent;
    private readonly DuplicateEvent _duplicate = duplicateEvent;

    public async Task InjectCombinedDropDuplicateAsync()
    {
        await using var c = new NpgsqlConnection(_conn);

        // Get two different transaction events to target for drop and duplicate.
        var targets = (await c.QueryAsync<EventTarget>(
            Queries.EventQueries.GetTwoDifferentTransactionEvents)).ToList();

        if (targets.Count < 2)
        {
            Console.WriteLine("Need at least two transaction events for combined drop+duplicate fault.");
            return;
        }

        var duplicateTarget = targets[0];
        var dropTarget = targets[1];

        await _duplicate.InjectAsync(
            duplicateTarget.Reference,
            duplicateTarget.Type,
            "combined_drop_duplicate");

        await _drop.InjectAsync(
            dropTarget.Reference,
            dropTarget.Type,
            "combined_drop_duplicate");

        Console.WriteLine("INJECTED combined drop+duplicate fault.");
        Console.WriteLine($"Duplicated: {duplicateTarget.Type} for {duplicateTarget.Reference}");
        Console.WriteLine($"Dropped: {dropTarget.Type} for {dropTarget.Reference}");
    }

    private class EventTarget
    {
        public string Reference { get; set; } = "";
        public string Type { get; set; } = "";
    }
}