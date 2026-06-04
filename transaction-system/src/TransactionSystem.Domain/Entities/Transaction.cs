using TransactionSystem.Domain.Enums;
using TransactionSystem.Domain.Exceptions;

namespace TransactionSystem.Domain.Entities;

public class Transaction : BaseEntity
{

    private Transaction(string accountNumber, TransactionType type, decimal amount, string? destAccountNumber)
    {
        if (string.IsNullOrWhiteSpace(accountNumber))
            throw new DomainException("Account Number is required.");

        if (amount <= 0)
            throw new DomainException("Transaction amount must be greater than zero.");

        AccountNumber = accountNumber;
        Type = type;
        Amount = amount;
        DestAccountNumber = destAccountNumber;
    }

    public string AccountNumber { get; private set; }
    public TransactionType Type { get; private set; }
    public decimal Amount { get; private set; }
    public string? DestAccountNumber { get; set; } = string.Empty;

    public static Transaction CreateCredit(string accountNumber, decimal amount, string? destAccountNumber = null)
    {
        return new Transaction(accountNumber, TransactionType.Credit, amount, destAccountNumber);
    }

    public static Transaction CreateDebit(string accountNumber, decimal amount, string? destAccountNumber = null)
    {
        return new Transaction(accountNumber, TransactionType.Debit, amount, destAccountNumber);
    }
}
