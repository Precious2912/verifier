namespace EventStore.Core.Events;

public record AccountDebited(string Reference, string DebitAccount, string CreditAccount, decimal Amount, DateTime OccurredAt);