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
    DetectionScorer scorer,
    ILogger<Worker> logger,
    IHostApplicationLifetime lifetime)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var loopMode = Environment.GetEnvironmentVariable("VERIFY_MODE") == "loop";
        var intervalMs = int.TryParse(Environment.GetEnvironmentVariable("VERIFY_INTERVAL_MS"), out var i)
            ? i : 3000;

        if (loopMode)
            logger.LogInformation("Verification running in LOOP mode (every {Interval}ms). Ctrl+C to stop.", intervalMs);

        do
        {
            try
            {
                var result = await RunPassAsync(ct);
                await LogAndScoreAsync(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Verification pass failed");
                if (!loopMode) throw;   // one-off: surface it; loop: log and keep going
            }

            if (!loopMode) break;

            logger.LogInformation("--- next pass in {Interval}ms ---", intervalMs);
            try { await Task.Delay(intervalMs, ct); }
            catch (TaskCanceledException) { break; }
        }
        while (!ct.IsCancellationRequested);

        if (!loopMode)
            lifetime.StopApplication();
    }

    private async Task LogAndScoreAsync(VerificationResult result)
    {
        var numeric = result.Numeric;
        var record = result.Record;
        var snapshot = result.Snapshot;

        var numericFlagged = numeric.Verdicts.Where(v => v.Status != VerdictStatus.Consistent).ToList();
        foreach (var v in numericFlagged)
            logger.LogWarning("Numeric flagged {Account}: stored={Stored}, crud={Crud}, event={Event} -> {Status}",
                v.AccountNumber, v.StoredBalance, v.CrudDerivedBalance, v.EventDerivedBalance, v.Status);
        logger.LogInformation("Numeric: {Consistent}/{Total} consistent",
            numeric.Verdicts.Count - numericFlagged.Count, numeric.Verdicts.Count);

        var recordAnomalies = record.Verdicts.Where(v => v.Status != RecordStatus.Matched).ToList();
        foreach (var v in recordAnomalies)
            logger.LogWarning("Record flagged {Reference} [{Type}/{Account}]: crud={Crud}, event={Event} -> {Status}",
                v.Reference, v.Type, v.Account, v.CrudAmount, v.EventAmount, v.Status);
        logger.LogInformation("Record-level: {Matched}/{Total} matched",
            record.Verdicts.Count(v => v.Status == RecordStatus.Matched), record.Verdicts.Count);

        if (snapshot.Verdict is not null)
        {
            var s = snapshot.Verdict;
            if (s.Status is SnapshotStatus.CountMismatch or SnapshotStatus.SumMismatch)
                logger.LogWarning("Snapshot slice: crud={CrudCount}/{CrudSum}, event={EventCount}/{EventSum} -> {Status}",
                    s.CrudCount, s.CrudGrossSum, s.EventCount, s.EventGrossSum, s.Status);
            else
                logger.LogInformation("Snapshot slice: crud={CrudCount}/{CrudSum}, event={EventCount}/{EventSum} -> {Status}",
                    s.CrudCount, s.CrudGrossSum, s.EventCount, s.EventGrossSum, s.Status);
        }

        logger.LogInformation("Numeric  — load={L:F1}ms compute={C:F1}ms",
            numeric.LoadTime.TotalMilliseconds, numeric.ComputeTime.TotalMilliseconds);
        logger.LogInformation("Record   — load={L:F1}ms compute={C:F1}ms",
            record.LoadTime.TotalMilliseconds, record.ComputeTime.TotalMilliseconds);
        logger.LogInformation("Snapshot — load={L:F1}ms compute={C:F1}ms",
            snapshot.LoadTime.TotalMilliseconds, snapshot.ComputeTime.TotalMilliseconds);

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
    }

    public async Task<VerificationResult> RunPassAsync(CancellationToken ct)
    {
        await verificationCheckpoint.EnsureTableAsync();
        var migrationCp = await migrationCheckpoint.GetAsync();

        var numeric = await RunNumericAsync();
        var record = await RunRecordAsync(migrationCp);
        var snapshot = await RunSnapshotAsync(migrationCp);

        return new VerificationResult(numeric, record, snapshot);
    }

    private async Task<NumericResult> RunNumericAsync()
    {
        var loadSw = Stopwatch.StartNew();
        var accounts = await crud.GetAccountsAsync();
        var transactions = await crud.GetTransactionsAsync();
        var eventRows = await events.GetEventsAsync();
        loadSw.Stop();

        var computeSw = Stopwatch.StartNew();
        var verdicts = NumericInvariant.Check(accounts, transactions, eventRows);
        computeSw.Stop();

        return new NumericResult(verdicts, loadSw.Elapsed, computeSw.Elapsed);
    }

    private async Task<RecordResult> RunRecordAsync(MigrationCheckpoint? migrationCp)
    {
        var loadSw = Stopwatch.StartNew();
        var transactions = await crud.GetTransactionsAsync();
        var eventRows = await events.GetEventsAsync();
        loadSw.Stop();

        var computeSw = Stopwatch.StartNew();
        var verdicts = RecordLevelInvariant.Check(transactions, eventRows, migrationCp);
        computeSw.Stop();

        return new RecordResult(verdicts, loadSw.Elapsed, computeSw.Elapsed);
    }

    private async Task<SnapshotResult> RunSnapshotAsync(MigrationCheckpoint? migrationCp)
    {
        if (migrationCp is null)
            return new SnapshotResult(null, TimeSpan.Zero, TimeSpan.Zero);

        var loadSw = Stopwatch.StartNew();
        var verificationCp = await verificationCheckpoint.GetAsync();
        var from = verificationCp?.LastCreatedAt ?? DateTime.MinValue.ToUniversalTime();
        var fromId = verificationCp?.LastId ?? Guid.Empty;

        var sliceTx = await crud.GetTransactionsInSliceAsync(
            from, fromId, migrationCp.LastCreatedAt, migrationCp.LastId);
        var refs = sliceTx.Select(t => t.Reference).Distinct().ToList();
        var sliceEvents = await events.GetEventsForReferencesAsync(refs);
        loadSw.Stop();

        var computeSw = Stopwatch.StartNew();
        var verdict = SnapshotInvariant.Check(from, migrationCp.LastCreatedAt, sliceTx, sliceEvents);
        computeSw.Stop();

        if (verdict.Status != SnapshotStatus.EmptySlice)
            await verificationCheckpoint.SetAsync(
                new VerificationCheckpoint(migrationCp.LastCreatedAt, migrationCp.LastId));

        return new SnapshotResult(verdict, loadSw.Elapsed, computeSw.Elapsed);
    }
}