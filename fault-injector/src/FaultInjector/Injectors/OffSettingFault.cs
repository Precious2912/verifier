using Dapper;
using FaultInjector.GroundTruth;
using Npgsql;

namespace FaultInjector.Injectors;

public class OffsettingFault(
    string eventsConnectionString,
    IncorrectAmount amountInjector,
    FaultLog log)
{
    private readonly string _conn = eventsConnectionString;
    private readonly IncorrectAmount _amount = amountInjector;
    private readonly FaultLog _log = log;

    public async Task InjectOffsettingAmountsAsync(decimal delta = 100m)
    {
        await using var c = new NpgsqlConnection(_conn);

        // Find an account with at least 2 events.
        var stream = await c.QuerySingleOrDefaultAsync<string?>(
            Queries.EventQueries.GetAccountWithMultipleEvents);
        if (stream is null) { Console.WriteLine("No account with >=2 events."); return; }

        // Get two events from that account.
        var events = (await c.QueryAsync<(string EventId, string Reference, string Type, decimal Amount)>(
            Queries.EventQueries.GetTwoEventsForStream, new { streamId = stream })).ToList();
        if (events.Count < 2) { Console.WriteLine($"Account {stream} has fewer than 2 events."); return; }

        var a = events[0];
        var b = events[1];

        // Corrupt event A by +delta, event B by -delta → they cancel in aggregate.
        await _amount.InjectAsync(a.Reference, a.Type, a.Amount + delta, "offsetting_amounts");
        await _amount.InjectAsync(b.Reference, b.Type, b.Amount - delta, "offsetting_amounts");

        Console.WriteLine($"INJECTED offsetting amounts on {stream}: " +
            $"{a.Reference} +{delta}, {b.Reference} -{delta} (net zero).");
    }
}