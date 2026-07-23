namespace MigrationService.Shared;

public static class Queries
{
    // Backfill
    public const string GetAccounts = """
        SELECT "AccountNumber", "AccountName", "CreatedAt"
        FROM "Accounts"
        ORDER BY "CreatedAt";
        """;

    public const string GetTransactions = """
        SELECT "Id", "Reference", "Type", "DebitAccount", "CreditAccount", "Amount", "CreatedAt"
        FROM "Transactions"
        ORDER BY "CreatedAt";
        """;

    // Polling
    public const string EnsureTableExists = """
        CREATE SCHEMA IF NOT EXISTS event_store;
        CREATE TABLE IF NOT EXISTS event_store.migration_checkpoint (
            id INT PRIMARY KEY,
            last_created_at TIMESTAMPTZ NOT NULL,
            last_id UUID NOT NULL);
        """;

    public const string GetCheckpoint = """
        SELECT last_created_at AS LastCreatedAt, last_id AS LastId
        FROM event_store.migration_checkpoint 
        WHERE id = 1;
        """;

    public const string SetCheckpoint = """
        INSERT INTO event_store.migration_checkpoint (id, last_created_at, last_id)
        VALUES (1, @LastCreatedAt, @LastId)
        ON CONFLICT (id) DO UPDATE
        SET last_created_at = EXCLUDED.last_created_at, last_id = EXCLUDED.last_id;
        """;

    public const string GetTransactionsAfterCheckpoint = """
        SELECT "Reference", "Type", "DebitAccount", "CreditAccount", "Amount", "CreatedAt", "Id"
        FROM "Transactions"
        WHERE ("CreatedAt", "Id") > (@lastCreatedAt, @lastId)
        ORDER BY "CreatedAt", "Id";
        """;
}