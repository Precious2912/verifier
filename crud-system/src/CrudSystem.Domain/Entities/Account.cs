using CrudSystem.Domain.Exceptions;

namespace CrudSystem.Domain.Entities;

public class Account : BaseEntity
{
    private Account()
    {
    }
    public Account(string accountNumber, string accountName)
    {
        if (string.IsNullOrWhiteSpace(accountNumber))
            throw new DomainException("Account number is required.");

        if (string.IsNullOrWhiteSpace(accountName))
            throw new DomainException("Account name is required.");

        AccountNumber = accountNumber;
        AccountName = accountName;
        Balance = 0;
    }

    public string AccountNumber { get; private set; } = string.Empty;

    public string AccountName { get; private set; } = string.Empty;

    public decimal Balance { get; private set; }

    public Transaction Credit(string reference, decimal amount, string debitAccount)
    {
        if (string.IsNullOrWhiteSpace(reference))
            throw new DomainException("Transaction reference is required.");

        if (amount <= 0)
            throw new DomainException("Credit amount must be greater than zero.");

        Balance += amount;
        MarkAsModified();

        var transaction = Transaction.CreateCredit(reference, debitAccount, amount, AccountNumber);

        return transaction;
    }

    public Transaction Debit(string reference, decimal amount, string creditAccount)
    {
        if (string.IsNullOrWhiteSpace(reference))
            throw new DomainException("Transaction reference is required.");

        if (amount <= 0)
            throw new DomainException("Debit amount must be greater than zero.");

        if (Balance < amount)
            throw new DomainException("Insufficient funds.");

        Balance -= amount;
        MarkAsModified();

        var transaction = Transaction.CreateDebit(reference, AccountNumber, amount, creditAccount);

        return transaction;
    }
}
