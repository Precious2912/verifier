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
    VerificationOptions options,
    ILogger<Worker> _logger,
    IHostApplicationLifetime lifetime) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (options.Concurrent)
        {
            _logger.LogInformation("Verification running in CONCURRENT mode (every {Interval}ms). Ctrl+C to stop.", options.IntervalMs);
        }

        do
        {
            try
            {
                var result = await RunPassAsync(ct);
                await LogAndScoreAsync(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Verification pass failed");
                if (!options.Concurrent) throw;
            }

            if (!options.Concurrent) break;

            //logger.LogInformation("--- next pass in {Interval}ms ---", options.IntervalMs);
            try
            {
                await Task.Delay(options.IntervalMs, ct);
            }

            catch (TaskCanceledException)
            {
                break;
            }
        }
        while (!ct.IsCancellationRequested);

        if (!options.Concurrent)
            lifetime.StopApplication();
    }
    private async Task LogAndScoreAsync(VerificationResult result)
    {
        var status = ComputeStatus(result);
        LogPassSummary(result, status);

        if (status.HasMismatches)
            LogMismatches(status);

        // Scoring is for controlled trials. In concurrent mode the evidence is the
        // live log; per-pass scoring would inflate the results table.
        if (!options.Concurrent)
            await ScoreAndLogResultsAsync(result);
    }
    private static PassStatus ComputeStatus(VerificationResult result)
    {
        var numericFlagged = result.Numeric.Verdicts
            .Where(v => v.Status != VerdictStatus.Consistent)
            .ToList();

        // PendingSync is not an anomaly: the record is legitimately not yet migrated.
        var recordAnomalies = result.Record.Verdicts
            .Where(v => v.Status != RecordStatus.Matched && v.Status != RecordStatus.PendingSync)
            .ToList();

        var pendingSync = result.Record.Verdicts.Count(v => v.Status == RecordStatus.PendingSync);

        return new PassStatus(numericFlagged, recordAnomalies, pendingSync, result.Snapshot.Verdict);
    }
    private void LogPassSummary(VerificationResult result, PassStatus status)
    {
        var n = result.Numeric;
        var r = result.Record;
        var s = result.Snapshot;

        _logger.LogInformation(
            "Pass: numeric {NumOk}/{NumTotal} consistent, record {RecOk}/{RecTotal} matched ({Pending} pending sync), " +
            "snapshot {Snapshot} | timing n={NL:F1}+{NC:F1}ms r={RL:F1}+{RC:F1}ms s={SL:F1}+{SC:F1}ms (load+compute)",
            n.Verdicts.Count - status.NumericFlagged.Count, n.Verdicts.Count,
            r.Verdicts.Count - status.RecordAnomalies.Count - status.PendingSync, r.Verdicts.Count, status.PendingSync,
            status.SnapshotVerdict?.Status.ToString() ?? "n/a",
            n.LoadTime.TotalMilliseconds, n.ComputeTime.TotalMilliseconds,
            r.LoadTime.TotalMilliseconds, r.ComputeTime.TotalMilliseconds,
            s.LoadTime.TotalMilliseconds, s.ComputeTime.TotalMilliseconds);
    }

    private void LogMismatches(PassStatus status)
    {
        foreach (var v in status.NumericFlagged)
            _logger.LogWarning("Numeric flagged {Account}: stored={Stored}, crud={Crud}, event={Event} -> {Status}",
                v.AccountNumber, v.StoredBalance, v.CrudDerivedBalance, v.EventDerivedBalance, v.Status);

        foreach (var v in status.RecordAnomalies)
            _logger.LogWarning("Record flagged {Reference} [{Type}/{Account}]: crud={Crud}, event={Event} -> {Status}",
                v.Reference, v.Type, v.Account, v.CrudAmount, v.EventAmount, v.Status);

        if (status.SnapshotMismatched)
        {
            var s = status.SnapshotVerdict!;
            _logger.LogWarning("Snapshot slice: crud={CrudCount}/{CrudSum}, event={EventCount}/{EventSum} -> {Status}",
                s.CrudCount, s.CrudGrossSum, s.EventCount, s.EventGrossSum, s.Status);
        }
    }

    private async Task ScoreAndLogResultsAsync(VerificationResult result)
    {
        var summary = await scorer.ScoreAsync(result);

        if (summary is null)
        {
            _logger.LogInformation("No active faults — nothing scored");
            return;
        }

        foreach (var d in summary.Detections)
            _logger.LogInformation("SCORING {FaultType} ({Ref}/{Account}): numeric={N}, record={R}, snapshot={S}",
                d.FaultType, d.TargetRef, d.TargetAccount, d.NumericCaught, d.RecordCaught, d.SnapshotCaught);

        _logger.LogInformation("False positives — numeric={NumFP}, record={RecFP}",
            summary.NumericFalsePositives, summary.RecordFalsePositives);
    }

    private record PassStatus(IReadOnlyList<NumericVerdict> NumericFlagged, IReadOnlyList<RecordVerdict> RecordAnomalies, int PendingSync, SnapshotVerdict? SnapshotVerdict)
    {
        public bool SnapshotMismatched =>
            SnapshotVerdict?.Status is SnapshotStatus.CountMismatch or SnapshotStatus.SumMismatch;

        public bool HasMismatches =>
            NumericFlagged.Count > 0 || RecordAnomalies.Count > 0 || SnapshotMismatched;
    }
    public async Task<VerificationResult> RunPassAsync(CancellationToken ct)
    {
        await verificationCheckpoint.EnsureTableAsync();
        var migrationCp = await migrationCheckpoint.GetAsync();

        // The compute time for the first approach is higher. 
        // Added this to prevent that by warming both connection pools. 
        await crud.WarmUpAsync();
        await events.WarmUpAsync();

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
public record VerificationOptions(bool Concurrent, int IntervalMs);
