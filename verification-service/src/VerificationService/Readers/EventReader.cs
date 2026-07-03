using Dapper;
using Npgsql;
using VerificationService.Models;

namespace VerificationService.Readers;

public class EventReader(string connectionString)
{
    private readonly string _connectionString = connectionString;

    public async Task<IReadOnlyList<EventRecord>> GetEventsAsync()
    {
        const string sql = """
            SELECT stream_id AS StreamId, type AS Type,
                   data ->> 'Reference' AS Reference,
                   data ->> 'DebitAccount' AS DebitAccount,
                   data ->> 'CreditAccount' AS CreditAccount,
                   (data ->> 'Amount')::numeric AS Amount,
                   (data ->> 'OccurredAt')::timestamptz AS OccurredAt
            FROM event_store.mt_events
            ORDER BY seq_id;
            """;
        await using var conn = new NpgsqlConnection(_connectionString);
        var rows = await conn.QueryAsync<EventRecord>(sql);
        return [.. rows]; //rows.ToList();
    }

    public async Task<IReadOnlyList<EventRecord>> GetEventsForReferencesAsync(
    IReadOnlyCollection<string> references)
    {
        if (references.Count == 0) return [];

        const string sql = """
        SELECT stream_id AS StreamId, type AS Type,
               data ->> 'Reference'                 AS Reference,
               data ->> 'DebitAccount'              AS DebitAccount,
               data ->> 'CreditAccount'             AS CreditAccount,
               (data ->> 'Amount')::numeric         AS Amount,
               (data ->> 'OccurredAt')::timestamptz AS OccurredAt
        FROM event_store.mt_events
        WHERE type IN ('account_debited', 'account_credited')
          AND data ->> 'Reference' = ANY(@references)
        ORDER BY seq_id;
        """;
        await using var conn = new NpgsqlConnection(_connectionString);
        var rows = await conn.QueryAsync<EventRecord>(
            sql, new { references = references.ToArray() });
        return rows.ToList();
    }
}