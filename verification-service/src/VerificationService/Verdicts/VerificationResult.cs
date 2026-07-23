namespace VerificationService.Verdicts;

public record NumericResult(IReadOnlyList<NumericVerdict> Verdicts, TimeSpan LoadTime, TimeSpan ComputeTime);
public record RecordResult(IReadOnlyList<RecordVerdict> Verdicts, TimeSpan LoadTime, TimeSpan ComputeTime);
public record SnapshotResult(SnapshotVerdict? Verdict, TimeSpan LoadTime, TimeSpan ComputeTime);
public record VerificationResult(NumericResult Numeric, RecordResult Record, SnapshotResult Snapshot);