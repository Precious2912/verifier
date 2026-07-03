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
    IHostApplicationLifetime lifetime)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var result = await RunPassAsync(ct);
        result.Print();

        // One-shot: stop the host once the pass is done.
        lifetime.StopApplication();
    }

    public async Task<VerificationResult> RunPassAsync(CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();

        await verificationCheckpoint.EnsureTableAsync();

        var accounts = await crud.GetAccountsAsync();
        var transactions = await crud.GetTransactionsAsync();
        var eventRows = await events.GetEventsAsync();
        var migrationCp = await migrationCheckpoint.GetAsync();

        // Numeric
        var numericVerdicts = numeric.Check(accounts, transactions, eventRows);

        // Record-level (checkpoint-aware)
        var recordVerdicts = recordLevel.Check(transactions, eventRows, migrationCp);

        // Snapshot (incremental slice between verification checkpoint and migration checkpoint)
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

            snapshotVerdict = snapshot.Check(
                from, migrationCp.LastCreatedAt, sliceTx, sliceEvents);

            if (snapshotVerdict.Status != SnapshotStatus.EmptySlice)
                await verificationCheckpoint.SetAsync(
                    new VerificationCheckpoint(migrationCp.LastCreatedAt, migrationCp.LastId));
        }

        stopwatch.Stop();
        return new VerificationResult(
            numericVerdicts, recordVerdicts, snapshotVerdict, stopwatch.Elapsed);
    }
}