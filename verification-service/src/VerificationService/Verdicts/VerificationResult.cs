namespace VerificationService.Verdicts;

// public record VerificationResult(
//     IReadOnlyList<NumericVerdict> Numeric,
//     IReadOnlyList<RecordVerdict> RecordLevel,
//     SnapshotVerdict? Snapshot,
//     TimeSpan Duration,
//     TimeSpan NumericDuration,
//     TimeSpan RecordDuration,
//     TimeSpan SnapshotDuration)
// { }

public record NumericResult(IReadOnlyList<NumericVerdict> Verdicts, TimeSpan LoadTime, TimeSpan ComputeTime);
public record RecordResult(IReadOnlyList<RecordVerdict> Verdicts, TimeSpan LoadTime, TimeSpan ComputeTime);
public record SnapshotResult(SnapshotVerdict? Verdict, TimeSpan LoadTime, TimeSpan ComputeTime);
public record VerificationResult(NumericResult Numeric, RecordResult Record, SnapshotResult Snapshot);