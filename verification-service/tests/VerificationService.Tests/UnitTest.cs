using VerificationService.Invariants;
using VerificationService.Models;
using VerificationService.Verdicts;
using VerificationService.Checkpoints;

namespace VerificationService.Tests;

public class NumericInvariantTests
{
    // Helper: a debit transaction lowers the debit account, a credit raises the credit account.
    private static CrudTransaction Debit(string reference, string account, decimal amount, string counterparty = "OTHER")
        => new()
        {
            Id = Guid.NewGuid(),
            Reference = reference,
            Type = "Debit",
            DebitAccount = account,
            CreditAccount = counterparty,
            Amount = amount,
            CreatedAt = DateTime.UtcNow
        };

    private static CrudTransaction Credit(string reference, string account, decimal amount, string counterparty = "OTHER")
        => new()
        {
            Id = Guid.NewGuid(),
            Reference = reference,
            Type = "Credit",
            DebitAccount = counterparty,
            CreditAccount = account,
            Amount = amount,
            CreatedAt = DateTime.UtcNow
        };

    private static EventRecord DebitEvent(string reference, string account, decimal amount)
        => new() { StreamId = account, Type = "account_debited", Reference = reference, Amount = amount };

    private static EventRecord CreditEvent(string reference, string account, decimal amount)
        => new() { StreamId = account, Type = "account_credited", Reference = reference, Amount = amount };

    [Fact]
    public void Consistent_When_Stored_Crud_And_Event_All_Agree()
    {
        var accounts = new[] { new CrudAccount { AccountNumber = "A", StoredBalance = 100m } };
        var transactions = new[] { Credit("R1", "A", 100m) };
        var events = new[] { CreditEvent("R1", "A", 100m) };

        var result = NumericInvariant.Check(accounts, transactions, events);

        Assert.Equal(VerdictStatus.Consistent, result.Single().Status);
    }

    [Fact]
    public void MigrationFault_When_Event_Derived_Differs_From_Crud()
    {
        // Stored matches CRUD (both 100), but the event says 50 -> migration fault.
        var accounts = new[] { new CrudAccount { AccountNumber = "A", StoredBalance = 100m } };
        var transactions = new[] { Credit("R1", "A", 100m) };
        var events = new[] { CreditEvent("R1", "A", 50m) };   // corrupted event amount

        var result = NumericInvariant.Check(accounts, transactions, events);

        Assert.Equal(VerdictStatus.MigrationFault, result.Single().Status);
    }

    [Fact]
    public void SourceIntegrityViolation_When_Stored_Differs_From_Derived()
    {
        // CRUD and event agree (100), but stored balance says 999 -> tampered balance.
        var accounts = new[] { new CrudAccount { AccountNumber = "A", StoredBalance = 999m } };
        var transactions = new[] { Credit("R1", "A", 100m) };
        var events = new[] { CreditEvent("R1", "A", 100m) };

        var result = NumericInvariant.Check(accounts, transactions, events);

        Assert.Equal(VerdictStatus.SourceIntegrityViolation, result.Single().Status);
    }

    [Fact]
    public void SourceIntegrity_Takes_Precedence_When_Stored_Differs_Even_If_Event_Also_Differs()
    {
        // Stored (999) != CRUD (100), AND event (50) != CRUD (100).
        // Classify checks stored != crud FIRST, so source-integrity wins.
        var accounts = new[] { new CrudAccount { AccountNumber = "A", StoredBalance = 999m } };
        var transactions = new[] { Credit("R1", "A", 100m) };
        var events = new[] { CreditEvent("R1", "A", 50m) };

        var result = NumericInvariant.Check(accounts, transactions, events);

        Assert.Equal(VerdictStatus.SourceIntegrityViolation, result.Single().Status);
    }
}

public class RecordLevelInvariantTests
{
    private static CrudTransaction Credit(string reference, string account, decimal amount)
        => new()
        {
            Id = Guid.NewGuid(),
            Reference = reference,
            Type = "Credit",
            DebitAccount = "OTHER",
            CreditAccount = account,
            Amount = amount,
            CreatedAt = DateTime.UtcNow
        };

    private static EventRecord CreditEvent(string reference, string account, decimal amount)
        => new() { StreamId = account, Type = "account_credited", Reference = reference, Amount = amount };

    // Checkpoint far in the future so all test transactions are "before" it (not PendingSync).
    private static MigrationCheckpoint FutureCheckpoint()
        => new(DateTime.UtcNow.AddYears(1), Guid.NewGuid());

    [Fact]
    public void Matched_When_Transaction_Has_Corresponding_Event()
    {
        var transactions = new[] { Credit("R1", "A", 100m) };
        var events = new[] { CreditEvent("R1", "A", 100m) };

        var result = RecordLevelInvariant.Check(transactions, events, FutureCheckpoint());

        Assert.Equal(RecordStatus.Matched, result.Single().Status);
    }

    [Fact]
    public void DroppedEvent_When_Transaction_Has_No_Event_Before_Checkpoint()
    {
        var transactions = new[] { Credit("R1", "A", 100m) };
        var events = Array.Empty<EventRecord>();   // event missing

        var result = RecordLevelInvariant.Check(transactions, events, FutureCheckpoint());

        Assert.Equal(RecordStatus.DroppedEvent, result.Single().Status);
    }

    [Fact]
    public void DuplicateEvent_When_Same_Event_Appears_Twice()
    {
        var transactions = new[] { Credit("R1", "A", 100m) };
        var events = new[] { CreditEvent("R1", "A", 100m), CreditEvent("R1", "A", 100m) };

        var result = RecordLevelInvariant.Check(transactions, events, FutureCheckpoint());

        Assert.Contains(result, v => v.Status == RecordStatus.DuplicateEvent);
    }

    [Fact]
    public void AmountMismatch_When_Event_Amount_Differs()
    {
        var transactions = new[] { Credit("R1", "A", 100m) };
        var events = new[] { CreditEvent("R1", "A", 75m) };   // wrong amount

        var result = RecordLevelInvariant.Check(transactions, events, FutureCheckpoint());

        Assert.Equal(RecordStatus.AmountMismatch, result.Single().Status);
    }

    [Fact]
    public void PendingSync_When_Transaction_Is_After_Checkpoint()
    {
        // Checkpoint in the past, transaction now -> transaction is unsynced, not dropped.
        var transactions = new[] { Credit("R1", "A", 100m) };
        var events = Array.Empty<EventRecord>();
        var pastCheckpoint = new MigrationCheckpoint(DateTime.UtcNow.AddYears(-1), Guid.Empty);

        var result = RecordLevelInvariant.Check(transactions, events, pastCheckpoint);

        Assert.Equal(RecordStatus.PendingSync, result.Single().Status);
    }
}

public class SnapshotInvariantTests
{
    private static CrudTransaction Tx(decimal amount)
        => new()
        {
            Id = Guid.NewGuid(),
            Reference = "R",
            Type = "Credit",
            DebitAccount = "O",
            CreditAccount = "A",
            Amount = amount,
            CreatedAt = DateTime.UtcNow
        };

    private static EventRecord Ev(decimal amount)
        => new() { StreamId = "A", Type = "account_credited", Reference = "R", Amount = amount };

    [Fact]
    public void Consistent_When_Count_And_Sum_Match()
    {
        var tx = new[] { Tx(100m), Tx(50m) };
        var ev = new[] { Ev(100m), Ev(50m) };

        var result = SnapshotInvariant.Check(DateTime.MinValue, DateTime.UtcNow, tx, ev);

        Assert.Equal(SnapshotStatus.Consistent, result.Status);
    }

    [Fact]
    public void CountMismatch_When_Event_Count_Differs()
    {
        var tx = new[] { Tx(100m), Tx(50m) };
        var ev = new[] { Ev(100m) };   // one event missing

        var result = SnapshotInvariant.Check(DateTime.MinValue, DateTime.UtcNow, tx, ev);

        Assert.Equal(SnapshotStatus.CountMismatch, result.Status);
    }

    [Fact]
    public void SumMismatch_When_Counts_Match_But_Amounts_Differ()
    {
        var tx = new[] { Tx(100m), Tx(50m) };   // sum 150
        var ev = new[] { Ev(100m), Ev(60m) };   // sum 160, same count

        var result = SnapshotInvariant.Check(DateTime.MinValue, DateTime.UtcNow, tx, ev);

        Assert.Equal(SnapshotStatus.SumMismatch, result.Status);
    }

    [Fact]
    public void EmptySlice_When_Nothing_To_Verify()
    {
        var result = SnapshotInvariant.Check(
            DateTime.MinValue, DateTime.UtcNow,
            Array.Empty<CrudTransaction>(), Array.Empty<EventRecord>());

        Assert.Equal(SnapshotStatus.EmptySlice, result.Status);
    }
}