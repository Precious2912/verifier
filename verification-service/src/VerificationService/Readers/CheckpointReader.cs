using Dapper;
using Npgsql;

namespace VerificationService.Readers;

public record VerificationCheckpoint(DateTime LastCreatedAt, Guid LastId);

public class CheckpointReader(string connectionString)
{
    private readonly string _connectionString = connectionString;

    public async Task<VerificationCheckpoint?> GetAsync()
    {
        const string sql = """
            SELECT last_created_at AS LastCreatedAt, last_id AS LastId
            FROM event_store.migration_checkpoint
            WHERE id = 1;
            """;
        await using var conn = new NpgsqlConnection(_connectionString);
        return await conn.QuerySingleOrDefaultAsync<VerificationCheckpoint>(sql);
    }
}