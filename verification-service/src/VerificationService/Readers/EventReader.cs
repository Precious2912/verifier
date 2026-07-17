using Dapper;
using Npgsql;
using VerificationService.Models;

namespace VerificationService.Readers;

public class EventReader(string connectionString)
{
    private readonly string _connectionString = connectionString;

    public async Task<IReadOnlyList<EventRecord>> GetEventsAsync()
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        var rows = await conn.QueryAsync<EventRecord>(Queries.GetEvents);
        return [.. rows];
    }

    public async Task<IReadOnlyList<EventRecord>> GetEventsForReferencesAsync(
    IReadOnlyCollection<string> references)
    {
        if (references.Count == 0) return [];
        await using var conn = new NpgsqlConnection(_connectionString);
        var rows = await conn.QueryAsync<EventRecord>(
            Queries.GetEventsForReferences, new { references = references.ToArray() });
        return [.. rows];
    }
}