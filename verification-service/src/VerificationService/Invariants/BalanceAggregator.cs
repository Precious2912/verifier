using VerificationService.Models;

namespace VerificationService.Invariants;

public static class BalanceAggregator
{
    // CRUD: +Amount to CreditAccount; -Amount to DebitAccount.
    public static Dictionary<string, decimal> FoldCrud(
        IEnumerable<CrudTransaction> transactions)
    {
        var balances = new Dictionary<string, decimal>();

        foreach (var t in transactions)
        {
            if (t.Type == "Credit")
                Add(balances, t.CreditAccount, t.Amount);
            else if (t.Type == "Debit")
                Add(balances, t.DebitAccount, -t.Amount);
        }

        return balances;
    }

    // Events: +Amount to account_credited stream; -Amount to account_debited stream.
    public static Dictionary<string, decimal> FoldEvents(
        IEnumerable<EventRecord> events)
    {
        var balances = new Dictionary<string, decimal>();

        foreach (var e in events)
        {
            if (e.Type == "account_credited" && e.Amount is { } credited)
                Add(balances, e.StreamId, credited);
            else if (e.Type == "account_debited" && e.Amount is { } debited)
                Add(balances, e.StreamId, -debited);
        }

        return balances;
    }

    private static void Add(Dictionary<string, decimal> map, string account, decimal amount)
    {
        map.TryGetValue(account, out var current);
        map[account] = current + amount;
    }
}