using Dapper;
using Npgsql;

namespace VerificationService.Verdicts;

public record FaultDetection(
    string FaultType,
    string? TargetRef,
    string? TargetAccount,
    bool NumericCaught,
    bool RecordCaught,
    bool SnapshotCaught);

public record ScoringSummary(
    int FaultCount,
    IReadOnlyList<FaultDetection> Detections,
    int NumericFalsePositives,
    int RecordFalsePositives,
    double NumericMs,
    double RecordMs,
    double SnapshotMs);

public class DetectionScorer(string eventsConnectionString)
{
    private readonly string _conn = eventsConnectionString;

    public async Task<ScoringSummary?> ScoreAsync(VerificationResult result)
    {
        await using var c = new NpgsqlConnection(_conn);

        // Read ALL active (unreverted) faults — one for isolated, several for compound.
        var faults = (await c.QueryAsync<(string FaultType, string? TargetRef, string? TargetAccount)>("""
            SELECT fault_type AS FaultType, target_ref AS TargetRef, target_account AS TargetAccount
            FROM evaluation.injected_faults
            WHERE reverted = FALSE
            ORDER BY injected_at;
            """)).ToList();

        if (faults.Count == 0)
        {
            return null;
        }

        // Pre-compute what each approach flagged.
        var numericFlagged = result.Numeric
            .Where(v => v.Status != VerdictStatus.Consistent)
            .Select(v => v.AccountNumber)
            .ToHashSet();

        var recordFlagged = result.RecordLevel
            .Where(v => v.Status != RecordStatus.Matched && v.Status != RecordStatus.PendingSync)
            .Select(v => v.Reference)
            .ToHashSet();

        var snapshotFlagged = result.Snapshot is not null &&
            result.Snapshot.Status is SnapshotStatus.CountMismatch or SnapshotStatus.SumMismatch;

        // The set of targets we DID inject (for false-positive counting).
        var injectedAccounts = faults.Select(f => f.TargetAccount).Where(a => a != null).ToHashSet();
        var injectedRefs = faults.Select(f => f.TargetRef).Where(r => r != null).ToHashSet();

        // Score each fault against its own target.
        var detections = new List<FaultDetection>();
        foreach (var f in faults)
        {
            var numericCaught = f.TargetAccount != null && numericFlagged.Contains(f.TargetAccount);
            var recordCaught = f.TargetRef != null && recordFlagged.Contains(f.TargetRef);
            // Snapshot is slice-level: it either flagged the slice or not (can't attribute per-fault).
            var snapshotCaught = snapshotFlagged;

            detections.Add(new FaultDetection(
                f.FaultType, f.TargetRef, f.TargetAccount,
                numericCaught, recordCaught, snapshotCaught));
        }

        // False positives: flagged things that were NOT injected targets.
        var numericFPs = numericFlagged.Count(a => !injectedAccounts.Contains(a));
        var recordFPs = recordFlagged.Count(r => !injectedRefs.Contains(r));

        var summary = new ScoringSummary(
            faults.Count, detections, numericFPs, recordFPs,
            result.NumericDuration.TotalMilliseconds,
            result.RecordDuration.TotalMilliseconds,
            result.SnapshotDuration.TotalMilliseconds);

        // Persist one row per fault.
        await EnsureResultsTableAsync(c);
        foreach (var d in detections)
        {
            await c.ExecuteAsync("""
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
                """, new
            {
                Id = Guid.NewGuid(),
                d.FaultType,
                d.TargetRef,
                d.TargetAccount,
                d.NumericCaught,
                d.RecordCaught,
                d.SnapshotCaught,
                NumericFPs = numericFPs,
                RecordFPs = recordFPs,
                NumericMs = summary.NumericMs,
                RecordMs = summary.RecordMs,
                SnapshotMs = summary.SnapshotMs,
                FaultCount = faults.Count,
                At = DateTime.UtcNow
            });
        }

        return summary;
    }

    private static async Task EnsureResultsTableAsync(NpgsqlConnection c)
    {
        await c.ExecuteAsync("""
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
            """);
    }
}