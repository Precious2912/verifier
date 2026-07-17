using Dapper;
using Npgsql;

namespace VerificationService.Checkpoints;

public record VerificationCheckpoint(DateTime LastCreatedAt, Guid LastId);

public class VerificationCheckpointStore(string connectionString)
{
    private readonly string _connectionString = connectionString;

    public async Task EnsureTableAsync()
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.ExecuteAsync(Queries.EnsureTableAsync);
    }

    public async Task<VerificationCheckpoint?> GetAsync()
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        return await conn.QuerySingleOrDefaultAsync<VerificationCheckpoint>(Queries.GetVerificationCheckpoint);
    }

    public async Task SetAsync(VerificationCheckpoint cp)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.ExecuteAsync(Queries.SetVerificationCheckpoint, cp);
    }
}