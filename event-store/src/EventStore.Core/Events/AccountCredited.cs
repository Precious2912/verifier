namespace EventStore.Core.Events;

public record AccountCredited(
    string Reference,
    string CreditAccount,
    string DebitAccount,
    decimal Amount,
    DateTime OccurredAt);