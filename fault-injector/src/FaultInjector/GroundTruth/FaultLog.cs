using Dapper;
using FaultInjector.Queries;
using Npgsql;

namespace FaultInjector.GroundTruth;

public record InjectedFault(
    Guid Id, string FaultType, string Tier,
    string? TargetRef, string? TargetAccount, string? TargetDetail,
    string? OriginalValue, string? InjectedValue,
    DateTime InjectedAt, bool Reverted);

public class FaultLog(string eventsConnectionString)
{
    private readonly string _conn = eventsConnectionString;

    public async Task EnsureSchemaAsync()
    {
        await using var conn = new NpgsqlConnection(_conn);
        await conn.ExecuteAsync(GroundTruthQueries.CreateInjectedFaultsTable);
    }

    public async Task RecordAsync(InjectedFault fault)
    {
        await using var conn = new NpgsqlConnection(_conn);
        await conn.ExecuteAsync(GroundTruthQueries.InsertInjectedFault, fault);
    }

    public async Task<InjectedFault?> GetActiveAsync()
    {
        await using var conn = new NpgsqlConnection(_conn);
        return await conn.QuerySingleOrDefaultAsync<InjectedFault>(GroundTruthQueries.GetActiveInjectedFault);
    }

    public async Task<IReadOnlyList<InjectedFault>> GetAllActiveAsync()
    {
        await using var conn = new NpgsqlConnection(_conn);
        return (await conn.QueryAsync<InjectedFault>(GroundTruthQueries.GetAllActiveInjectedFaults)).ToList();
    }

    public async Task MarkRevertedAsync(Guid id)
    {
        await using var conn = new NpgsqlConnection(_conn);
        await conn.ExecuteAsync(
            Queries.GroundTruthQueries.MarkRevertedInjectedFault, new { id });
    }
}