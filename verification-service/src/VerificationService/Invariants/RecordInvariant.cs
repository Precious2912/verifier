using VerificationService.Checkpoints;
using VerificationService.Models;
using VerificationService.Verdicts;

namespace VerificationService.Invariants;

public class RecordLevelInvariant
{
    public static IReadOnlyList<RecordVerdict> Check(
        IReadOnlyList<CrudTransaction> transactions,
        IReadOnlyList<EventRecord> events,
        MigrationCheckpoint? checkpoint)
    {
        // Lookup of events by their matching key: (Reference, Type, Account).
        // Event type maps back to CRUD type: account_debited -> Debit, account_credited -> Credit.
        // Account is the event's stream (which is the row's "own" account).
        var eventLookup = new Dictionary<string, List<EventRecord>>();

        foreach (var e in events)
        {
            var crudType = e.Type switch
            {
                "account_debited" => "Debit",
                "account_credited" => "Credit",
                _ => null   // added this to handle account_created events. It has no matching transaction.
            };
            if (crudType is null) continue;

            var key = Key(e.Reference ?? "", crudType, e.StreamId);
            if (!eventLookup.TryGetValue(key, out var list))
                eventLookup[key] = list = new List<EventRecord>();
            list.Add(e);
        }

        var verdicts = new List<RecordVerdict>();
        var matchedEventKeys = new HashSet<string>();

        // Check 1: every CRUD transaction should have exactly one matching event
        // (unless it's newer than the checkpoint (legitimately pending)).
        foreach (var t in transactions)
        {
            var ownAccount = t.Type == "Debit" ? t.DebitAccount : t.CreditAccount;
            var key = Key(t.Reference, t.Type, ownAccount);

            if (eventLookup.TryGetValue(key, out var matches) && matches.Count > 0)
            {
                var evt = matches[0];
                matchedEventKeys.Add(key);

                var status = evt.Amount == t.Amount
                    ? RecordStatus.Matched
                    : RecordStatus.AmountMismatch;

                verdicts.Add(new RecordVerdict(
                    t.Reference, t.Type, ownAccount, t.Amount, evt.Amount, status));

                // A second event under the same key is a duplicate.
                if (matches.Count > 1)
                    verdicts.Add(new RecordVerdict(
                        t.Reference, t.Type, ownAccount, t.Amount, matches[1].Amount,
                        RecordStatus.DuplicateEvent));
            }
            else
            {
                // No event. Is it a genuine drop, or just not synced yet?
                var status = IsAfterCheckpoint(t, checkpoint)
                    ? RecordStatus.PendingSync
                    : RecordStatus.DroppedEvent;

                verdicts.Add(new RecordVerdict(
                    t.Reference, t.Type, ownAccount, t.Amount, null, status));
            }
        }

        /*         // Check 2: any event with no matching CRUD row. Spurious events
                foreach (var (key, list) in eventLookup)
                {
                    if (matchedEventKeys.Contains(key)) continue;

                    foreach (var e in list)
                    {
                        var crudType = e.Type == "account_debited" ? "Debit" : "Credit";
                        verdicts.Add(new RecordVerdict(
                            e.Reference ?? "", crudType, e.StreamId, null, e.Amount,
                            RecordStatus.SpuriousEvent));
                    }
                } */

        return verdicts;
    }

    private static string Key(string reference, string type, string account)
        => $"{reference}|{type}|{account}";

    // A CRUD transaction is "after" the checkpoint (legitimately unsynced) if its
    // (CreatedAt, Id) is strictly greater than the checkpoint's (LastCreatedAt, LastId). 
    // If the checkpoint is null, everything should be considered after it.
    private static bool IsAfterCheckpoint(CrudTransaction t, MigrationCheckpoint? cp)
    {
        if (cp is null) return true; //nothing synced yet -> everything is pending
        if (t.CreatedAt > cp.LastCreatedAt) return true;
        if (t.CreatedAt < cp.LastCreatedAt) return false;
        return t.Id.CompareTo(cp.LastId) > 0; //tie on timestamp -> compare Id
    }
}