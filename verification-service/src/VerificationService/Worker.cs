using System.Diagnostics;
using VerificationService.Checkpoints;
using VerificationService.Invariants;
using VerificationService.Readers;
using VerificationService.Verdicts;

namespace VerificationService;

public class Worker(
    CrudReader crud,
    EventReader events,
    MigrationCheckpointReader migrationCheckpoint,
    VerificationCheckpointStore verificationCheckpoint,
    NumericInvariant numeric,
    RecordLevelInvariant recordLevel,
    SnapshotInvariant snapshot,
    DetectionScorer scorer,
    ILogger<Worker> logger,
    IHostApplicationLifetime lifetime)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var result = await RunPassAsync(ct);

        // --- Numeric ---
        var numericFlagged = result.Numeric.Where(v => v.Status != VerdictStatus.Consistent).ToList();
        foreach (var v in numericFlagged)
            logger.LogWarning("Numeric flagged {Account}: stored={Stored}, crud={Crud}, event={Event} -> {Status}",
                v.AccountNumber, v.StoredBalance, v.CrudDerivedBalance, v.EventDerivedBalance, v.Status);
        logger.LogInformation("Numeric: {Consistent}/{Total} consistent",
            result.Numeric.Count - numericFlagged.Count, result.Numeric.Count);

        // --- Record-level ---
        var recordAnomalies = result.RecordLevel.Where(v => v.Status != RecordStatus.Matched).ToList();
        foreach (var v in recordAnomalies)
            logger.LogWarning("Record flagged {Reference} [{Type}/{Account}]: crud={Crud}, event={Event} -> {Status}",
                v.Reference, v.Type, v.Account, v.CrudAmount, v.EventAmount, v.Status);
        logger.LogInformation("Record-level: {Matched}/{Total} matched",
            result.RecordLevel.Count(v => v.Status == RecordStatus.Matched), result.RecordLevel.Count);

        // --- Snapshot ---
        if (result.Snapshot is not null)
        {
            if (result.Snapshot.Status is SnapshotStatus.CountMismatch or SnapshotStatus.SumMismatch)
                logger.LogWarning("Snapshot slice: crud={CrudCount}/{CrudSum}, event={EventCount}/{EventSum} -> {Status}",
                    result.Snapshot.CrudCount, result.Snapshot.CrudGrossSum,
                    result.Snapshot.EventCount, result.Snapshot.EventGrossSum, result.Snapshot.Status);
            else
                logger.LogInformation("Snapshot slice: crud={CrudCount}/{CrudSum}, event={EventCount}/{EventSum} -> {Status}",
                    result.Snapshot.CrudCount, result.Snapshot.CrudGrossSum,
                    result.Snapshot.EventCount, result.Snapshot.EventGrossSum, result.Snapshot.Status);
        }

        // --- Timing ---
        logger.LogInformation("Pass timing — numeric={N:F1}ms record={R:F1}ms snapshot={S:F1}ms total={T:F1}ms",
            result.NumericDuration.TotalMilliseconds, result.RecordDuration.TotalMilliseconds,
            result.SnapshotDuration.TotalMilliseconds, result.Duration.TotalMilliseconds);

        // --- Scoring ---
        var summary = await scorer.ScoreAsync(result);
        if (summary is not null)
        {
            foreach (var d in summary.Detections)
                logger.LogInformation("SCORING {FaultType} (ref={Ref}, acct={Account}): numeric={N}, record={R}, snapshot={S}",
                    d.FaultType, d.TargetRef, d.TargetAccount, d.NumericCaught, d.RecordCaught, d.SnapshotCaught);
            logger.LogInformation("False positives — numeric={NumFP}, record={RecFP}",
                summary.NumericFalsePositives, summary.RecordFalsePositives);
        }
        else
        {
            logger.LogInformation("No active faults — nothing to score");
        }

        lifetime.StopApplication();
    }

    public async Task<VerificationResult> RunPassAsync(CancellationToken ct)
    {
        var totalSw = Stopwatch.StartNew();

        await verificationCheckpoint.EnsureTableAsync();

        var accounts = await crud.GetAccountsAsync();
        var transactions = await crud.GetTransactionsAsync();
        var eventRows = await events.GetEventsAsync();
        var migrationCp = await migrationCheckpoint.GetAsync();

        var numericSw = Stopwatch.StartNew();
        var numericVerdicts = numeric.Check(accounts, transactions, eventRows);
        numericSw.Stop();

        var recordSw = Stopwatch.StartNew();
        var recordVerdicts = recordLevel.Check(transactions, eventRows, migrationCp);
        recordSw.Stop();

        var snapshotSw = Stopwatch.StartNew();
        SnapshotVerdict? snapshotVerdict = null;
        if (migrationCp is not null)
        {
            var verificationCp = await verificationCheckpoint.GetAsync();
            var from = verificationCp?.LastCreatedAt ?? DateTime.MinValue.ToUniversalTime();
            var fromId = verificationCp?.LastId ?? Guid.Empty;

            var sliceTx = await crud.GetTransactionsInSliceAsync(
                from, fromId, migrationCp.LastCreatedAt, migrationCp.LastId);
            var refs = sliceTx.Select(t => t.Reference).Distinct().ToList();
            var sliceEvents = await events.GetEventsForReferencesAsync(refs);

            snapshotVerdict = snapshot.Check(from, migrationCp.LastCreatedAt, sliceTx, sliceEvents);

            if (snapshotVerdict.Status != SnapshotStatus.EmptySlice)
                await verificationCheckpoint.SetAsync(
                    new VerificationCheckpoint(migrationCp.LastCreatedAt, migrationCp.LastId));
        }
        snapshotSw.Stop();

        totalSw.Stop();

        return new VerificationResult(
            numericVerdicts, recordVerdicts, snapshotVerdict,
            totalSw.Elapsed,
            numericSw.Elapsed, recordSw.Elapsed, snapshotSw.Elapsed);
    }
}