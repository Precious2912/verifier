namespace EventStore.Core.Events;

public record AccountCreated(string AccountNumber, string AccountName, DateTime OccurredAt);