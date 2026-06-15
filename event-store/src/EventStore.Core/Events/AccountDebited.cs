namespace EventStore.Core.Events;

public record AccountDebited(
    string Reference,
    string CreditAccount,
    string DebitAccount,
    decimal Amount,
    DateTime OccurredAt);