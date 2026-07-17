namespace FaultInjector.Queries;

public static class GroundTruthQueries
{
    // Schema creation for injected faults
    public const string CreateInjectedFaultsTable = """
            CREATE SCHEMA IF NOT EXISTS evaluation;
            CREATE TABLE IF NOT EXISTS evaluation.injected_faults (
                id UUID PRIMARY KEY,
                fault_type TEXT NOT NULL,
                tier TEXT NOT NULL,
                target_ref TEXT,
                target_account TEXT,
                target_detail TEXT,
                original_value TEXT,
                injected_value TEXT,
                injected_at TIMESTAMPTZ NOT NULL,
                reverted BOOLEAN NOT NULL DEFAULT FALSE
            );
            """;

    public const string InsertInjectedFault = """
            INSERT INTO evaluation.injected_faults
                (id, fault_type, tier, target_ref, target_account, target_detail,
                 original_value, injected_value, injected_at, reverted)
            VALUES
                (@Id, @FaultType, @Tier, @TargetRef, @TargetAccount, @TargetDetail,
                 @OriginalValue, @InjectedValue, @InjectedAt, @Reverted);
            """;

    public const string GetActiveInjectedFault = """
            SELECT id AS Id, fault_type AS FaultType, tier AS Tier,
                   target_ref AS TargetRef, target_account AS TargetAccount,
                   target_detail AS TargetDetail,
                   original_value AS OriginalValue, injected_value AS InjectedValue,
                   injected_at AS InjectedAt, reverted AS Reverted
            FROM evaluation.injected_faults
            WHERE reverted = FALSE
            ORDER BY injected_at DESC LIMIT 1;
            """;

    public const string GetAllActiveInjectedFaults = """
            SELECT id AS Id, fault_type AS FaultType, tier AS Tier,
                   target_ref AS TargetRef, target_account AS TargetAccount,
                   target_detail AS TargetDetail,
                   original_value AS OriginalValue, injected_value AS InjectedValue,
                   injected_at AS InjectedAt, reverted AS Reverted
            FROM evaluation.injected_faults
            WHERE reverted = FALSE
            ORDER BY injected_at;
            """;

    public const string MarkRevertedInjectedFault = """UPDATE evaluation.injected_faults SET reverted = TRUE WHERE id = @id""";


}