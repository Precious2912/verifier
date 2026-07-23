using Marten;
using MigrationService.Crud;
using MigrationService.Shared;

namespace MigrationService.Polling;

public class SyncWorker(Reader crudReader, CheckpointStore checkpointStore, IDocumentStore eventStore, ILogger<SyncWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await checkpointStore.EnsureTableExistsAsync();

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await PollOnceAsync(ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Sync poll failed; will retry next interval.");
            }

            await Task.Delay(Interval, ct);
        }
    }

    private async Task PollOnceAsync(CancellationToken ct)
    {
        var cp = await checkpointStore.GetCheckpointAsync()
                 ?? new Checkpoint(DateTime.MinValue.ToUniversalTime(), Guid.Empty);

        var newRows = await crudReader.GetTransactionsSinceAsync(cp.LastCreatedAt, cp.LastId);
        if (newRows.Count == 0) return;

        await using var session = eventStore.LightweightSession();

        foreach (var t in newRows)
        {
            var mapped = EventMapper.Map(t);
            if (mapped is null)
            {
                logger.LogWarning("Unknown type '{Type}' ref {Ref}", t.Type, t.Reference);
                continue;
            }
            // Stream already exists from backfill. Append, not StartStream.
            session.Events.Append(mapped.Value.StreamKey, mapped.Value.Event);
        }

        await session.SaveChangesAsync(ct);

        var last = newRows[^1];
        await checkpointStore.SetCheckpointAsync(new Checkpoint(last.CreatedAt, last.Id));

        logger.LogInformation("Synced {Count} new transactions.", newRows.Count);
    }
}