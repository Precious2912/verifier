using TransactionSystem.Domain.Enums;
using TransactionSystem.Domain.Exceptions;

namespace TransactionSystem.Domain.Entities;

public class Transaction : BaseEntity
{
    public Guid AccountId { get; private set; }
    public TransactionType Type { get; private set; }
    public decimal Amount { get; private set; }
    public string? Description { get; private set; }

    private Transaction()
    {
    }

    private Transaction(
        Guid accountId,
        TransactionType type,
        decimal amount,
        string? description)
    {
        if (accountId == Guid.Empty)
            throw new DomainException("Account ID is required.");

        if (amount <= 0)
            throw new DomainException("Transaction amount must be greater than zero.");

        AccountId = accountId;
        Type = type;
        Amount = amount;
        Description = description;
    }

    public static Transaction CreateCredit(
        Guid accountId,
        decimal amount,
        string? description = null)
    {
        return new Transaction(
            accountId,
            TransactionType.Credit,
            amount,
            description);
    }

    public static Transaction CreateDebit(
        Guid accountId,
        decimal amount,
        string? description = null)
    {
        return new Transaction(
            accountId,
            TransactionType.Debit,
            amount,
            description);
    }
}
