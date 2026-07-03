namespace VerificationService.Verdicts;

public enum SnapshotStatus
{
    Consistent,
    CountMismatch, // dropped or duplicate events in the slice
    SumMismatch, // amount corruption somewhere in the slice
    EmptySlice // nothing new to verify
}

public record SnapshotVerdict(
    DateTime FromCreatedAt, DateTime ToCreatedAt,
    int CrudCount, int EventCount,
    decimal CrudGrossSum, decimal EventGrossSum,
    SnapshotStatus Status);