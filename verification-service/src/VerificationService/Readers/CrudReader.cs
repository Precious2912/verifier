using Dapper;
using Npgsql;
using VerificationService.Models;

namespace VerificationService.Readers;

public class CrudReader(string connectionString)
{
    private readonly string _connectionString = connectionString;

    public async Task<IReadOnlyList<CrudAccount>> GetAccountsAsync()
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        var rows = await conn.QueryAsync<CrudAccount>(Queries.GetAccountsAsync);
        return [.. rows];
    }

    public async Task<IReadOnlyList<CrudTransaction>> GetTransactionsAsync()
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        var rows = await conn.QueryAsync<CrudTransaction>(Queries.GetTransactionsAsync);
        return [.. rows];
    }

    public async Task<IReadOnlyList<CrudTransaction>> GetTransactionsInSliceAsync(
    DateTime fromCreatedAt, Guid fromId,
    DateTime toCreatedAt, Guid toId)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        var rows = await conn.QueryAsync<CrudTransaction>(Queries.GetTransactionsInSliceAsync, new { fromCreatedAt, fromId, toCreatedAt, toId });
        return [.. rows];
    }
}