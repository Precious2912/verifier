namespace VerificationService;

public static class Queries
{
        // Migration query
        public const string GetMigrationCheckpoint = """
            SELECT last_created_at AS LastCreatedAt, last_id AS LastId
            FROM event_store.migration_checkpoint
            WHERE id = 1;
            """;

        // Verification query

        // Schema creation for verification results
        public const string CreateVerificationCheckpointTable = """
            CREATE SCHEMA IF NOT EXISTS event_store;

            CREATE TABLE IF NOT EXISTS event_store.verification_checkpoint (
                id INT PRIMARY KEY,
                last_created_at TIMESTAMPTZ NOT NULL,
                last_id UUID NOT NULL
            );
            """;

        public const string GetVerificationCheckpoint = """
            SELECT last_created_at AS LastCreatedAt, last_id AS LastId
            FROM event_store.verification_checkpoint WHERE id = 1;
            """;

        public const string SetVerificationCheckpoint = """
            INSERT INTO event_store.verification_checkpoint (id, last_created_at, last_id)
            VALUES (1, @LastCreatedAt, @LastId)
            ON CONFLICT (id) DO UPDATE
            SET last_created_at = EXCLUDED.last_created_at,
                last_id = EXCLUDED.last_id;
            """;

        // CRUD queries
        public const string GetAccounts = """
            SELECT "AccountNumber" AS AccountNumber, "Balance" AS StoredBalance
            FROM "Accounts";
            """;

        public const string GetTransactions = """
            SELECT "Id" AS Id, "Reference" AS Reference, "Type" AS Type, "DebitAccount" AS DebitAccount, "CreditAccount" AS CreditAccount, 
            "Amount" AS Amount, "CreatedAt" AS CreatedAt
            FROM "Transactions";
            """;

        public const string GetTransactionsInSlice = """
        SELECT "Reference" AS Reference, "Type" AS Type,
               "DebitAccount" AS DebitAccount, "CreditAccount" AS CreditAccount,
               "Amount" AS Amount, "CreatedAt" AS CreatedAt, "Id" AS Id
        FROM "Transactions"
        WHERE ("CreatedAt", "Id") >  (@fromCreatedAt, @fromId)
          AND ("CreatedAt", "Id") <= (@toCreatedAt, @toId)
        ORDER BY "CreatedAt", "Id";
        """;

        // Event queries
        public const string GetEvents = """
            SELECT stream_id AS StreamId, type AS Type,
                   data ->> 'Reference' AS Reference,
                   data ->> 'DebitAccount' AS DebitAccount,
                   data ->> 'CreditAccount' AS CreditAccount,
                   (data ->> 'Amount')::numeric AS Amount,
                   (data ->> 'OccurredAt')::timestamptz AS OccurredAt
            FROM event_store.mt_events
            ORDER BY seq_id;
            """;

        public const string GetEventsForReferences = """
        SELECT stream_id AS StreamId, type AS Type,
               data ->> 'Reference' AS Reference,
               data ->> 'DebitAccount' AS DebitAccount,
               data ->> 'CreditAccount' AS CreditAccount,
               (data ->> 'Amount')::numeric AS Amount,
               (data ->> 'OccurredAt')::timestamptz AS OccurredAt
        FROM event_store.mt_events
        WHERE type IN ('account_debited', 'account_credited')
          AND data ->> 'Reference' = ANY(@references)
        ORDER BY seq_id;
        """;

        // Detection result queries
        public const string InsertResults = """
                INSERT INTO evaluation.detection_results
                    (id, fault_type, target_ref, target_account,
                     numeric_caught, record_caught, snapshot_caught,
                     numeric_fps, record_fps,
                     numeric_ms, record_ms, snapshot_ms,
                     fault_count, scored_at)
                VALUES
                    (@Id, @FaultType, @TargetRef, @TargetAccount,
                     @NumericCaught, @RecordCaught, @SnapshotCaught,
                     @NumericFPs, @RecordFPs, @NumericMs, @RecordMs, @SnapshotMs,
                     @FaultCount, @At);
                """;

        public const string GetFaults = """
            SELECT fault_type AS FaultType, target_ref AS TargetRef, target_account AS TargetAccount
            FROM evaluation.injected_faults
            WHERE reverted = FALSE
            ORDER BY injected_at;
            """;

        // Schema creation for evaluation results
        public const string CreateDetectionResultsTable = """
            CREATE TABLE IF NOT EXISTS evaluation.detection_results (
                id UUID PRIMARY KEY,
                fault_type TEXT NOT NULL,
                target_ref TEXT,
                target_account TEXT,
                numeric_caught BOOLEAN NOT NULL,
                record_caught BOOLEAN NOT NULL,
                snapshot_caught BOOLEAN NOT NULL,
                numeric_fps INT NOT NULL,
                record_fps INT NOT NULL,
                numeric_ms DOUBLE PRECISION NOT NULL,
                record_ms DOUBLE PRECISION NOT NULL,
                snapshot_ms DOUBLE PRECISION NOT NULL,
                fault_count INT NOT NULL,
                scored_at TIMESTAMPTZ NOT NULL
            );
            """;
}