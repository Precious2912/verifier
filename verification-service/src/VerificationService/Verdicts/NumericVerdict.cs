namespace VerificationService.Verdicts;

public enum VerdictStatus
{
    Consistent,
    SourceIntegrityViolation, // stored balance != CRUD-derived (like manual edit)
    MigrationFault // CRUD-derived != event-derived (dropped/duplicate event)
}

public record NumericVerdict(string AccountNumber, decimal StoredBalance, decimal CrudDerivedBalance, decimal EventDerivedBalance, VerdictStatus Status)
{
    public bool IsConsistent => Status == VerdictStatus.Consistent;
}