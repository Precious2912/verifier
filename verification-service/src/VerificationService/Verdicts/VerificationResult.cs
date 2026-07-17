using VerificationService.Verdicts;

namespace VerificationService;

public record VerificationResult(
    IReadOnlyList<NumericVerdict> Numeric,
    IReadOnlyList<RecordVerdict> RecordLevel,
    SnapshotVerdict? Snapshot,
    TimeSpan Duration,              // total
    TimeSpan NumericDuration,       // NEW
    TimeSpan RecordDuration,        // NEW
    TimeSpan SnapshotDuration)      // NEW
{
    public void Print()
    {
        Console.WriteLine("----- Numeric Invariant -----");
        var flaggedNumeric = Numeric.Where(v => v.Status != VerdictStatus.Consistent).ToList();
        foreach (var v in flaggedNumeric)
            Console.WriteLine($"{v.AccountNumber}: stored={v.StoredBalance}, " +
                $"crud={v.CrudDerivedBalance}, event={v.EventDerivedBalance} -> {v.Status}");
        Console.WriteLine($"Consistent: {Numeric.Count - flaggedNumeric.Count}/{Numeric.Count}" +
            (flaggedNumeric.Count > 0 ? $"  ({flaggedNumeric.Count} flagged)" : ""));

        Console.WriteLine("----- Record-Level Invariant -----");
        var anomalies = RecordLevel.Where(v => v.Status != RecordStatus.Matched).ToList();
        foreach (var v in anomalies)
            Console.WriteLine(
                $"{v.Reference} [{v.Type}/{v.Account}]: crud={v.CrudAmount}, " +
                $"event={v.EventAmount} -> {v.Status}");
        Console.WriteLine(
            $"Matched: {RecordLevel.Count(v => v.Status == RecordStatus.Matched)}/{RecordLevel.Count}");

        Console.WriteLine("----- Snapshot Invariant -----");
        if (Snapshot is null)
            Console.WriteLine("No migration checkpoint yet — nothing synced.");
        else
            Console.WriteLine(
                $"Slice ({Snapshot.FromCreatedAt:o} -> {Snapshot.ToCreatedAt:o}]: " +
                $"crud={Snapshot.CrudCount}/{Snapshot.CrudGrossSum}, " +
                $"event={Snapshot.EventCount}/{Snapshot.EventGrossSum} -> {Snapshot.Status}");

        Console.WriteLine($"Pass completed in {Duration.TotalMilliseconds:F0} ms.");
        Console.WriteLine($"\nTiming — numeric: {NumericDuration.TotalMilliseconds:F1}ms, " +
    $"record: {RecordDuration.TotalMilliseconds:F1}ms, " +
    $"snapshot: {SnapshotDuration.TotalMilliseconds:F1}ms, " +
    $"total: {Duration.TotalMilliseconds:F1}ms");
    }
}