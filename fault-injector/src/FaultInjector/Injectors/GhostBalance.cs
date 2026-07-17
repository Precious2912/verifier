using Dapper;
using FaultInjector.GroundTruth;
using Npgsql;

namespace FaultInjector.Injectors;

public class GhostBalance(string crudConnectionString, FaultLog log)
{
    private readonly string _conn = crudConnectionString;
    private readonly FaultLog _log = log;

    public async Task InjectAsync(string? accountNumber = null, decimal? newBalance = null)
    {
        await using var c = new NpgsqlConnection(_conn);

        if (accountNumber is null)
        {
            accountNumber = await c.QuerySingleOrDefaultAsync<string?>(Queries.CrudQueries.GetRandomAccount);
            if (accountNumber is null) { Console.WriteLine("No account to edit."); return; }
        }

        var current = await c.ExecuteScalarAsync<decimal>(Queries.CrudQueries.GetAccountBalance, new { a = accountNumber });

        var corrupted = newBalance ?? (current + 5000m);

        await c.ExecuteAsync(Queries.CrudQueries.UpdateAccountBalance,
            new { a = accountNumber, b = corrupted });

        await _log.RecordAsync(new InjectedFault(
            Guid.NewGuid(), "GhostBalance", "SourceIntegrity",
            accountNumber, accountNumber, $"account {accountNumber}",
            current.ToString(), corrupted.ToString(), DateTime.UtcNow, false));

        Console.WriteLine($"INJECTED ghost-balance: {accountNumber} {current} -> {corrupted}.");
    }

    public async Task RevertAsync(InjectedFault fault)
    {
        await using var c = new NpgsqlConnection(_conn);
        await c.ExecuteAsync(Queries.CrudQueries.UpdateAccountBalance,
            new { a = fault.TargetRef, b = decimal.Parse(fault.OriginalValue!) });
        await _log.MarkRevertedAsync(fault.Id);
        Console.WriteLine($"REVERTED ghost-balance: restored {fault.OriginalValue} for {fault.TargetRef}.");
    }
}