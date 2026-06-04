using TransactionSystem.Domain.Exceptions;

namespace TransactionSystem.Domain.Entities;

public class Account : BaseEntity
{
    private readonly List<Transaction> _transactions = [];

    public Account(string accountNumber, string accountName)
    {
        if (string.IsNullOrWhiteSpace(accountNumber))
            throw new DomainException("Account number is required.");

        if (string.IsNullOrWhiteSpace(accountName))
            throw new DomainException("Owner name is required.");

        AccountNumber = accountNumber;
        AccountName = accountName;
        Balance = 0;
    }

    public string AccountNumber { get; private set; } = string.Empty;

    public string AccountName { get; private set; } = string.Empty;

    public decimal Balance { get; private set; }

    public IReadOnlyCollection<Transaction> Transactions => _transactions.AsReadOnly();

    public Transaction Credit(decimal amount, string? destAccountNumber = null)
    {
        if (amount <= 0)
            throw new DomainException("Credit amount must be greater than zero.");

        Balance += amount;
        MarkAsModified();

        var transaction = Transaction.CreateCredit(AccountNumber, amount, destAccountNumber);

        _transactions.Add(transaction);

        return transaction;
    }

    public Transaction Debit(decimal amount, string? destAccountNumber = null)
    {
        if (amount <= 0)
            throw new DomainException("Debit amount must be greater than zero.");

        if (Balance < amount)
            throw new DomainException("Insufficient funds.");

        Balance -= amount;
        MarkAsModified();

        var transaction = Transaction.CreateDebit(AccountNumber, amount, destAccountNumber);

        _transactions.Add(transaction);

        return transaction;
    }
}
