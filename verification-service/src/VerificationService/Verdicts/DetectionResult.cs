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
    int RecordFalsePositives);

public class DetectionScorer(string eventsConnectionString, string crudConnectionString)
{
    private readonly string _eventConn = eventsConnectionString;
    private readonly string _crudConn = crudConnectionString;

    public async Task<ScoringSummary?> ScoreAsync(VerificationResult result)
    {
        try
        {
            await using var c = new NpgsqlConnection(_eventConn);

            var faults = (await c.QueryAsync<(string FaultType, string Scenario, string? TargetRef, string? TargetAccount)>(
                Queries.GetFaults)).ToList();

            if (faults.Count == 0)
                return null;

            int scale;
            await using (var crud = new NpgsqlConnection(_crudConn))
                scale = await crud.ExecuteScalarAsync<int>(Queries.CountTransactions);

            var scenario = faults[0].Scenario;

            // --- what each approach flagged (note the new .Verdicts / .Verdict shape) ---
            var numericFlagged = result.Numeric.Verdicts
                .Where(v => v.Status != VerdictStatus.Consistent)
                .Select(v => v.AccountNumber)
                .ToHashSet();

            var recordFlagged = result.Record.Verdicts
                .Where(v => v.Status != RecordStatus.Matched && v.Status != RecordStatus.PendingSync)
                .Select(v => v.Reference)
                .ToHashSet();

            var snapshotFlagged = result.Snapshot.Verdict is not null &&
                result.Snapshot.Verdict.Status is SnapshotStatus.CountMismatch or SnapshotStatus.SumMismatch;

            var injectedAccounts = faults.Select(f => f.TargetAccount).Where(a => a != null).ToHashSet();
            var injectedRefs = faults.Select(f => f.TargetRef).Where(r => r != null).ToHashSet();

            var detections = new List<FaultDetection>();
            foreach (var f in faults)
            {
                var numericCaught = f.TargetAccount != null && numericFlagged.Contains(f.TargetAccount);
                var recordCaught = f.TargetRef != null && recordFlagged.Contains(f.TargetRef);
                var snapshotCaught = snapshotFlagged;

                detections.Add(new FaultDetection(
                    f.FaultType, f.TargetRef, f.TargetAccount,
                    numericCaught, recordCaught, snapshotCaught));
            }

            var numericFPs = numericFlagged.Count(a => !injectedAccounts.Contains(a));
            var recordFPs = recordFlagged.Count(r => !injectedRefs.Contains(r));

            var summary = new ScoringSummary(faults.Count, detections, numericFPs, recordFPs);

            await EnsureResultsTableAsync(c);
            foreach (var d in detections)
            {
                await c.ExecuteAsync(Queries.InsertResults, new
                {
                    Id = Guid.NewGuid(),
                    Scenario = scenario,
                    Scale = scale,
                    d.FaultType,
                    d.TargetRef,
                    d.TargetAccount,
                    d.NumericCaught,
                    d.RecordCaught,
                    d.SnapshotCaught,
                    NumericFPs = numericFPs,
                    RecordFPs = recordFPs,
                    // per-approach load + compute
                    NumericLoadMs = result.Numeric.LoadTime.TotalMilliseconds,
                    NumericComputeMs = result.Numeric.ComputeTime.TotalMilliseconds,
                    RecordLoadMs = result.Record.LoadTime.TotalMilliseconds,
                    RecordComputeMs = result.Record.ComputeTime.TotalMilliseconds,
                    SnapshotLoadMs = result.Snapshot.LoadTime.TotalMilliseconds,
                    SnapshotComputeMs = result.Snapshot.ComputeTime.TotalMilliseconds,
                    FaultCount = faults.Count,
                    At = DateTime.UtcNow
                });
            }

            return summary;
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01")
        {
            return null;
        }
    }

    private static async Task EnsureResultsTableAsync(NpgsqlConnection c)
    {
        await c.ExecuteAsync(Queries.CreateDetectionResultsTable);
    }
}