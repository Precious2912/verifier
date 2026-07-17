using Dapper;
using Npgsql;

namespace VerificationService.Checkpoints;

public record MigrationCheckpoint(DateTime LastCreatedAt, Guid LastId);

public class MigrationCheckpointReader(string connectionString)
{
    private readonly string _connectionString = connectionString;

    public async Task<MigrationCheckpoint?> GetAsync()
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        return await conn.QuerySingleOrDefaultAsync<MigrationCheckpoint>(Queries.GetMigrationCheckpoint);
    }
}