namespace MigrationService.Crud;

public static class Queries
{
    public const string GetAccounts = """
        SELECT "AccountNumber", "AccountName", "CreatedAt"
        FROM "Accounts"
        ORDER BY "CreatedAt";
        """;

    public const string GetTransactions = """
        SELECT "Reference", "Type", "DebitAccount", "CreditAccount",
               "Amount", "CreatedAt"
        FROM "Transactions"
        ORDER BY "CreatedAt";
        """;
}