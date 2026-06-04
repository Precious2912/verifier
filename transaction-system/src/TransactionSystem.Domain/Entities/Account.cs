using TransactionSystem.Domain.Exceptions;

namespace TransactionSystem.Domain.Entities;

public class Account : BaseEntity
{
    private readonly List<Transaction> _transactions = new();

    public string AccountNumber { get; private set; } = string.Empty;

    public string OwnerName { get; private set; } = string.Empty;

    public decimal Balance { get; private set; }

    public IReadOnlyCollection<Transaction> Transactions => _transactions.AsReadOnly();

    private Account()
    {
    }

    public Account(
        string accountNumber,
        string ownerName,
        decimal openingBalance)
    {
        if (string.IsNullOrWhiteSpace(accountNumber))
            throw new DomainException("Account number is required.");

        if (string.IsNullOrWhiteSpace(ownerName))
            throw new DomainException("Owner name is required.");

        if (openingBalance < 0)
            throw new DomainException("Opening balance cannot be negative.");

        AccountNumber = accountNumber;
        OwnerName = ownerName;
        Balance = openingBalance;

        if (openingBalance > 0)
        {
            var openingTransaction = Transaction.CreateCredit(
                Id,
                openingBalance,
                "Opening balance");

            _transactions.Add(openingTransaction);
        }
    }

    public Transaction Credit(decimal amount, string? description = null)
    {
        if (amount <= 0)
            throw new DomainException("Credit amount must be greater than zero.");

        Balance += amount;
        MarkAsModified();

        var transaction = Transaction.CreateCredit(
            Id,
            amount,
            description);

        _transactions.Add(transaction);

        return transaction;
    }

    public Transaction Debit(decimal amount, string? description = null)
    {
        if (amount <= 0)
            throw new DomainException("Debit amount must be greater than zero.");

        if (Balance < amount)
            throw new DomainException("Insufficient funds.");

        Balance -= amount;
        MarkAsModified();

        var transaction = Transaction.CreateDebit(
            Id,
            amount,
            description);

        _transactions.Add(transaction);

        return transaction;
    }
}
