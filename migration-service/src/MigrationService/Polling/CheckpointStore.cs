using Dapper;
using MigrationService.Shared;
using Npgsql;

namespace MigrationService.Polling;

public record Checkpoint(DateTime LastCreatedAt, Guid LastId);

public class CheckpointStore(string eventConnectionString)
{
    private readonly string _conn = eventConnectionString;

    public async Task EnsureTableExistsAsync()
    {
        await using var c = new NpgsqlConnection(_conn);
        await c.ExecuteAsync(Queries.EnsureTableExists);
    }

    public async Task<Checkpoint?> GetCheckpointAsync()
    {
        await using var c = new NpgsqlConnection(_conn);
        return await c.QuerySingleOrDefaultAsync<Checkpoint>(Queries.GetCheckpoint);
    }

    public async Task SetCheckpointAsync(Checkpoint cp)
    {
        await using var c = new NpgsqlConnection(_conn);
        await c.ExecuteAsync(Queries.SetCheckpoint, cp);
    }
}