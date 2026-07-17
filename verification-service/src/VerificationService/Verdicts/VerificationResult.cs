namespace VerificationService.Verdicts;

public record VerificationResult(
    IReadOnlyList<NumericVerdict> Numeric,
    IReadOnlyList<RecordVerdict> RecordLevel,
    SnapshotVerdict? Snapshot,
    TimeSpan Duration,
    TimeSpan NumericDuration,
    TimeSpan RecordDuration,
    TimeSpan SnapshotDuration)
{ }