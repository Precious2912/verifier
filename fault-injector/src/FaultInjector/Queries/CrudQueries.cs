namespace FaultInjector.Queries;

public static class CrudQueries
{
    // Ghost balance (Accounts)
    public const string GetRandomAccount = """
        SELECT "AccountNumber" FROM "Accounts"
        ORDER BY random() LIMIT 1;
        """;

    public const string GetAccountBalance = """
        SELECT "Balance" FROM "Accounts"
        WHERE "AccountNumber" = @a;
        """;

    // Overwrite an account's balance — fault: ghostbalance, mode: inject (corrupt) and revert (restore).
    public const string UpdateAccountBalance = """
        UPDATE "Accounts" SET "Balance" = @b
        WHERE "AccountNumber" = @a;
        """;

    // Ghost transaction
    public const string GetRandomTransaction = """
        SELECT "Reference" AS Reference, "Type" AS Type, "Id"::text AS Id,
               "Amount" AS Amount, "DebitAccount" AS Debit, "CreditAccount" AS Credit
        FROM "Transactions"
        ORDER BY random() LIMIT 1;
        """;

    public const string FindTransactionByReference = """
        SELECT "Id"::text AS Id, "Amount" AS Amount,
               "DebitAccount" AS Debit, "CreditAccount" AS Credit
        FROM "Transactions"
        WHERE "Reference" = @reference AND "Type" = @type
        LIMIT 1;
        """;

    // Overwrite a transaction's amount — fault: ghosttransaction, mode: inject (corrupt) and revert (restore).
    public const string UpdateTransactionAmount = """
        UPDATE "Transactions" SET "Amount" = @amt
        WHERE "Id" = @id::uuid;
        """;
}