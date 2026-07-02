using VerificationService.Models;
using VerificationService.Verdicts;

namespace VerificationService.Invariants;

public class NumericInvariant
{
    public IReadOnlyList<NumericVerdict> Check(
        IReadOnlyList<CrudAccount> accounts,
        IReadOnlyList<CrudTransaction> transactions,
        IReadOnlyList<EventRecord> events)
    {
        var crudDerived = BalanceAggregator.FoldCrud(transactions);
        var eventDerived = BalanceAggregator.FoldEvents(events);

        var verdicts = new List<NumericVerdict>();

        foreach (var account in accounts)
        {
            var stored = account.StoredBalance;
            crudDerived.TryGetValue(account.AccountNumber, out var crud);
            eventDerived.TryGetValue(account.AccountNumber, out var evt);

            var status = Classify(stored, crud, evt);
            verdicts.Add(new NumericVerdict(
                account.AccountNumber, stored, crud, evt, status));
        }

        return verdicts;
    }

    private static VerdictStatus Classify(decimal stored, decimal crud, decimal evt)
    {
        // Migration fault takes precedence: the two stores disagree.
        if (crud != evt)
            return VerdictStatus.MigrationFault;

        // Stores agree, but stored balance doesn't match its own transactions.
        if (stored != crud)
            return VerdictStatus.SourceIntegrityViolation;

        return VerdictStatus.Consistent;
    }
}