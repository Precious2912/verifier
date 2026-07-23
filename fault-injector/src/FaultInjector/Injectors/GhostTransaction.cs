using Dapper;
using FaultInjector.GroundTruth;
using Npgsql;

namespace FaultInjector.Injectors;

public class GhostTransaction(string crudConnectionString, FaultLog log)
{
    private readonly string _conn = crudConnectionString;
    private readonly FaultLog _log = log;

    public async Task InjectAsync(string? reference = null, string? type = null, decimal? newAmount = null, string scenario = "single_ghosttxn")
    {
        await using var c = new NpgsqlConnection(_conn);

        string id, origAmount, debit, credit;

        if (reference is null)
        {
            var t = await c.QuerySingleOrDefaultAsync<(string? Reference, string? Type, string? Id, decimal Amount, string? Debit, string? Credit)>(
                Queries.CrudQueries.GetRandomTransaction
            );
            if (t.Reference is null) { Console.WriteLine("No transaction to edit."); return; }
            reference = t.Reference; type = t.Type; id = t.Id!;
            origAmount = t.Amount.ToString(); debit = t.Debit!; credit = t.Credit!;
        }
        else
        {
            var t = await c.QuerySingleOrDefaultAsync<(string Id, decimal Amount, string Debit, string Credit)>(
                Queries.CrudQueries.FindTransactionByReference, new { reference, type });
            if (t.Id is null) { Console.WriteLine($"No {type} txn for {reference}."); return; }
            id = t.Id; origAmount = t.Amount.ToString(); debit = t.Debit; credit = t.Credit;
        }

        var affectedAccount = type == "Debit" ? debit : credit;
        var corrupted = newAmount ?? (decimal.Parse(origAmount) + 100m);

        await c.ExecuteAsync(Queries.CrudQueries.UpdateTransactionAmount,
            new { id, amt = corrupted });

        // Allow overriding the scenario via environment variable for testing batch faults
        var envScenario = Environment.GetEnvironmentVariable("SCENARIO");
        if (!string.IsNullOrEmpty(envScenario))
        {
            scenario = envScenario;
        }

        await _log.RecordAsync(new InjectedFault(
            Guid.NewGuid(), "GhostTransaction", "SourceIntegrity", scenario,
            reference, affectedAccount, $"{type} txn {id}",
            origAmount, corrupted.ToString(), DateTime.UtcNow, false));

        Console.WriteLine($"INJECTED ghost-txn: {type} {reference} {origAmount} -> {corrupted}.");
    }

    public async Task RevertAsync(InjectedFault fault)
    {
        var txnId = fault.TargetDetail!.Split("txn ")[^1].Trim();
        await using var c = new NpgsqlConnection(_conn);
        await c.ExecuteAsync(Queries.CrudQueries.UpdateTransactionAmount,
            new { id = txnId, amt = decimal.Parse(fault.OriginalValue!) });
        await _log.MarkRevertedAsync(fault.Id);
        Console.WriteLine($"REVERTED ghost-txn: restored {fault.OriginalValue}.");
    }
}