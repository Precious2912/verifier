using EventStore.Core.Events;
using Marten;
using MigrationService.Crud;
using MigrationService.Polling;
using MigrationService.Shared;

namespace MigrationService.Backfill;

public class BackfillRunner(Reader crudReader, CheckpointStore checkpointStore, IDocumentStore eventStore, ILogger<BackfillRunner> logger)
{
    public async Task RunAsync(CancellationToken ct)
    {
        var accounts = await crudReader.GetAccountsAsync();
        var transactions = await crudReader.GetTransactionsAsync();

        logger.LogInformation($"Backfill starting: {accounts.Count} accounts, {transactions.Count} transactions.");

        // Group all events by the stream (account number) they belong to.
        // AccountCreated -> that account's stream.
        // A Debit row -> the DebitAccount's stream.
        // A Credit row -> the CreditAccount's stream.
        var streams = new Dictionary<string, List<object>>();

        void Add(string streamKey, object @event)
        {
            if (!streams.TryGetValue(streamKey, out var list))
            {
                list = []; // List<object>
                streams[streamKey] = list;
            }
            list.Add(@event);
        }

        foreach (var a in accounts)
            Add(a.AccountNumber,
                new AccountCreated(a.AccountNumber, a.AccountName, a.CreatedAt));

        foreach (var t in transactions)
        {
            var mapped = EventMapper.Map(t);
            if (mapped is null)
            {
                logger.LogWarning($"Unknown transaction type '{t.Type}' for ref {t.Reference}");
                continue;
            }
            Add(mapped.Value.StreamKey, mapped.Value.Event);
        }

        // Start each stream once with its full set of events.
        await using var session = eventStore.LightweightSession();
        foreach (var (streamKey, events) in streams)
            session.Events.StartStream(streamKey, events.ToArray());

        await session.SaveChangesAsync(ct);

        // Seed the checkpoint so sync picks up only transactions newer than these.
        if (transactions.Count > 0)
        {
            var last = transactions
                .OrderBy(t => t.CreatedAt)
                .ThenBy(t => t.Id)
                .Last();

            await checkpointStore.EnsureTableExistsAsync();
            await checkpointStore.SetCheckpointAsync(new Checkpoint(last.CreatedAt, last.Id));

            logger.LogInformation(
                "Checkpoint seeded at {CreatedAt} / {Id}.", last.CreatedAt, last.Id);
        }

        logger.LogInformation($"Backfill complete: {streams.Count} streams written.");
    }
}