using Dapper;
using Npgsql;

namespace VerificationService.Checkpoints;

public record VerificationCheckpoint(DateTime LastCreatedAt, Guid LastId);

public class VerificationCheckpointStore(string connectionString)
{
    private readonly string _connectionString = connectionString;

    public async Task EnsureTableAsync()
    {
        const string ddl = """
            CREATE SCHEMA IF NOT EXISTS event_store;

            CREATE TABLE IF NOT EXISTS event_store.verification_checkpoint (
                id              INT PRIMARY KEY,
                last_created_at TIMESTAMPTZ NOT NULL,
                last_id         UUID NOT NULL
            );
            """;
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.ExecuteAsync(ddl);
    }

    public async Task<VerificationCheckpoint?> GetAsync()
    {
        const string sql = """
            SELECT last_created_at AS LastCreatedAt, last_id AS LastId
            FROM event_store.verification_checkpoint WHERE id = 1;
            """;
        await using var conn = new NpgsqlConnection(_connectionString);
        return await conn.QuerySingleOrDefaultAsync<VerificationCheckpoint>(sql);
    }

    public async Task SetAsync(VerificationCheckpoint cp)
    {
        const string sql = """
            INSERT INTO event_store.verification_checkpoint (id, last_created_at, last_id)
            VALUES (1, @LastCreatedAt, @LastId)
            ON CONFLICT (id) DO UPDATE
            SET last_created_at = EXCLUDED.last_created_at,
                last_id = EXCLUDED.last_id;
            """;
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.ExecuteAsync(sql, cp);
    }
}