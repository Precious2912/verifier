namespace VerificationService.Verdicts;

public enum RecordStatus
{
    Matched, //CRUD row and event agree
    DroppedEvent, //CRUD row at/before checkpoint has no event
    DuplicateEvent, //multiple events
    // SpuriousEvent, //event has no matching CRUD row
    AmountMismatch, //CRUD records and events matched but amounts differ
    PendingSync //CRUD row newer than checkpoint — legitimately not yet migrated
}

public record RecordVerdict(
    string Reference,
    string Type,
    string Account,
    decimal? CrudAmount,
    decimal? EventAmount,
    RecordStatus Status);