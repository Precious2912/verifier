using Dapper;
using Npgsql;
using VerificationService.Models;

namespace VerificationService.Readers;

public class CrudReader(string connectionString)
{
    private readonly string _connectionString = connectionString;

    public async Task<IReadOnlyList<CrudAccount>> GetAccountsAsync()
    {
        const string sql = """
            SELECT "AccountNumber" AS AccountNumber, "Balance" AS StoredBalance
            FROM "Accounts";
            """;
        await using var conn = new NpgsqlConnection(_connectionString);
        var rows = await conn.QueryAsync<CrudAccount>(sql);
        return rows.ToList();
    }

    public async Task<IReadOnlyList<CrudTransaction>> GetTransactionsAsync()
    {
        const string sql = """
            SELECT "Id" AS Id, "Reference" AS Reference, "Type" AS Type, "DebitAccount" AS DebitAccount, "CreditAccount" AS CreditAccount, 
            "Amount" AS Amount, "CreatedAt" AS CreatedAt
            FROM "Transactions";
            """;
        //await using var conn = _db.Open();
        await using var conn = new NpgsqlConnection(_connectionString);
        var rows = await conn.QueryAsync<CrudTransaction>(sql);
        return rows.ToList();
    }

    public async Task<IReadOnlyList<CrudTransaction>> GetTransactionsInSliceAsync(
    DateTime fromCreatedAt, Guid fromId,
    DateTime toCreatedAt, Guid toId)
    {
        const string sql = """
        SELECT "Reference" AS Reference, "Type" AS Type,
               "DebitAccount" AS DebitAccount, "CreditAccount" AS CreditAccount,
               "Amount" AS Amount, "CreatedAt" AS CreatedAt, "Id" AS Id
        FROM "Transactions"
        WHERE ("CreatedAt", "Id") >  (@fromCreatedAt, @fromId)
          AND ("CreatedAt", "Id") <= (@toCreatedAt, @toId)
        ORDER BY "CreatedAt", "Id";
        """;
        await using var conn = new NpgsqlConnection(_connectionString);
        var rows = await conn.QueryAsync<CrudTransaction>(
            sql, new { fromCreatedAt, fromId, toCreatedAt, toId });
        return rows.ToList();
    }
}