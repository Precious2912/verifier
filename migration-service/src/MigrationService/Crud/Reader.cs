using Dapper;
using Npgsql;

namespace MigrationService.Crud;

public class Reader(string connectionString)
{
    private readonly string _connectionString = connectionString;

    public async Task<IReadOnlyList<CrudAccount>> GetAccountsAsync()
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        var rows = await conn.QueryAsync<CrudAccount>(Queries.GetAccounts);
        return [.. rows]; // returns rows to list
    }

    public async Task<IReadOnlyList<CrudTransaction>> GetTransactionsAsync()
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        var rows = await conn.QueryAsync<CrudTransaction>(Queries.GetTransactions);
        return [.. rows];
    }
}