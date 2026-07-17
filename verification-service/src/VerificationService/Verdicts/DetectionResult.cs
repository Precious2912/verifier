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
        var faults = (await c.QueryAsync<(string FaultType, string? TargetRef, string? TargetAccount)>(Queries.GetFaults)).ToList();

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

        // The set of targets injected (for false-positive counting).
        var injectedAccounts = faults.Select(f => f.TargetAccount).Where(a => a != null).ToHashSet();
        var injectedRefs = faults.Select(f => f.TargetRef).Where(r => r != null).ToHashSet();

        // Score each fault against its own target.
        var detections = new List<FaultDetection>();
        foreach (var (FaultType, TargetRef, TargetAccount) in faults)
        {
            var numericCaught = TargetAccount != null && numericFlagged.Contains(TargetAccount);
            var recordCaught = TargetRef != null && recordFlagged.Contains(TargetRef);
            // Snapshot is slice-level: it either flagged the slice or not (can't attribute per-fault).
            var snapshotCaught = snapshotFlagged;

            detections.Add(new FaultDetection(
                FaultType, TargetRef, TargetAccount,
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
            await c.ExecuteAsync(Queries.InsertResults, new
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
        await c.ExecuteAsync(Queries.CreateDetectionResultsTable);
    }
}