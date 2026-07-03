using VerificationService.Models;
using VerificationService.Verdicts;

namespace VerificationService.Invariants;

public class SnapshotInvariant
{
    public SnapshotVerdict Check(
        DateTime from, DateTime to,
        IReadOnlyList<CrudTransaction> sliceTransactions,
        IReadOnlyList<EventRecord> sliceEvents)
    {
        if (sliceTransactions.Count == 0 && sliceEvents.Count == 0)
            return new SnapshotVerdict(from, to, 0, 0, 0, 0, SnapshotStatus.EmptySlice);

        var crudCount = sliceTransactions.Count;
        var eventCount = sliceEvents.Count;

        var crudSum = sliceTransactions.Sum(t => t.Amount);
        var eventSum = sliceEvents.Sum(e => e.Amount ?? 0m);

        var status =
            crudCount != eventCount ? SnapshotStatus.CountMismatch
          : crudSum != eventSum ? SnapshotStatus.SumMismatch
          : SnapshotStatus.Consistent;

        return new SnapshotVerdict(from, to, crudCount, eventCount, crudSum, eventSum, status);
    }
}