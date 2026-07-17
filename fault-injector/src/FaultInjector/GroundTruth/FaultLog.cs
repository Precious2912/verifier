using Dapper;
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
        const string ddl = """
            CREATE SCHEMA IF NOT EXISTS evaluation;
            CREATE TABLE IF NOT EXISTS evaluation.injected_faults (
                id             UUID PRIMARY KEY,
                fault_type     TEXT NOT NULL,
                tier           TEXT NOT NULL,
                target_ref     TEXT,
                target_account TEXT,
                target_detail  TEXT,
                original_value TEXT,
                injected_value TEXT,
                injected_at    TIMESTAMPTZ NOT NULL,
                reverted       BOOLEAN NOT NULL DEFAULT FALSE
            );
            """;
        await using var c = new NpgsqlConnection(_conn);
        await c.ExecuteAsync(ddl);
    }

    public async Task RecordAsync(InjectedFault f)
    {
        const string sql = """
            INSERT INTO evaluation.injected_faults
                (id, fault_type, tier, target_ref, target_account, target_detail,
                 original_value, injected_value, injected_at, reverted)
            VALUES
                (@Id, @FaultType, @Tier, @TargetRef, @TargetAccount, @TargetDetail,
                 @OriginalValue, @InjectedValue, @InjectedAt, @Reverted);
            """;
        await using var c = new NpgsqlConnection(_conn);
        await c.ExecuteAsync(sql, f);
    }

    public async Task<InjectedFault?> GetActiveAsync()
    {
        const string sql = """
            SELECT id AS Id, fault_type AS FaultType, tier AS Tier,
                   target_ref AS TargetRef, target_account AS TargetAccount,
                   target_detail AS TargetDetail,
                   original_value AS OriginalValue, injected_value AS InjectedValue,
                   injected_at AS InjectedAt, reverted AS Reverted
            FROM evaluation.injected_faults
            WHERE reverted = FALSE
            ORDER BY injected_at DESC LIMIT 1;
            """;
        await using var c = new NpgsqlConnection(_conn);
        return await c.QuerySingleOrDefaultAsync<InjectedFault>(sql);
    }

    public async Task<IReadOnlyList<InjectedFault>> GetAllActiveAsync()
    {
        const string sql = """
            SELECT id AS Id, fault_type AS FaultType, tier AS Tier,
                   target_ref AS TargetRef, target_account AS TargetAccount,
                   target_detail AS TargetDetail,
                   original_value AS OriginalValue, injected_value AS InjectedValue,
                   injected_at AS InjectedAt, reverted AS Reverted
            FROM evaluation.injected_faults
            WHERE reverted = FALSE
            ORDER BY injected_at;
            """;
        await using var c = new NpgsqlConnection(_conn);
        return (await c.QueryAsync<InjectedFault>(sql)).ToList();
    }

    public async Task MarkRevertedAsync(Guid id)
    {
        await using var c = new NpgsqlConnection(_conn);
        await c.ExecuteAsync(
            "UPDATE evaluation.injected_faults SET reverted = TRUE WHERE id = @id;", new { id });
    }
}