using EventStore.Core.Events;
using Marten;
using MigrationService.Crud;

namespace MigrationService.Backfill;

public class BackfillRunner(Reader crudReader, IDocumentStore eventStore, ILogger<BackfillRunner> logger)
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
            if (t.Type == "Debit")
                Add(t.DebitAccount,
                    new AccountDebited(
                        Reference: t.Reference,
                        DebitAccount: t.DebitAccount,
                        CreditAccount: t.CreditAccount,
                        Amount: t.Amount,
                        OccurredAt: t.CreatedAt));
            else if (t.Type == "Credit")
                Add(t.CreditAccount,
                    new AccountCredited(
                        Reference: t.Reference,
                        DebitAccount: t.DebitAccount,
                        CreditAccount: t.CreditAccount,
                        Amount: t.Amount,
                        OccurredAt: t.CreatedAt));
            else
                logger.LogWarning($"Unknown transaction type '{t.Type}' for ref {t.Reference}");
        }

        // Start each stream once with its full set of events.
        await using var session = eventStore.LightweightSession();
        foreach (var (streamKey, events) in streams)
            session.Events.StartStream(streamKey, events.ToArray());

        await session.SaveChangesAsync(ct);

        logger.LogInformation($"Backfill complete: {streams.Count} streams written.");
    }
}