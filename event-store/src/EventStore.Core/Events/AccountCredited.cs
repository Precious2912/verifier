namespace EventStore.Core.Events;

public record AccountCredited(string Reference, string DebitAccount, string CreditAccount, decimal Amount, DateTime OccurredAt);