using CrudSystem.Domain.Enums;
using CrudSystem.Domain.Exceptions;

namespace CrudSystem.Domain.Entities;

public class Transaction : BaseEntity
{
    private Transaction()
    {
    }
    private Transaction(string reference, TransactionType type, string debitAccount, decimal amount, string creditAccount)
    {
        if (string.IsNullOrWhiteSpace(reference))
            throw new DomainException("Transaction reference is required.");

        if (string.IsNullOrWhiteSpace(debitAccount))
            throw new DomainException("Debit Account is required.");

        if (amount <= 0)
            throw new DomainException("Transaction amount must be greater than zero.");

        if (string.IsNullOrWhiteSpace(creditAccount))
            throw new DomainException("Credit Account is required.");

        Reference = reference;
        Type = type;
        DebitAccount = debitAccount;
        Amount = amount;
        CreditAccount = creditAccount;
    }

    public string Reference { get; private set; } = string.Empty;
    public string DebitAccount { get; private set; } = string.Empty;
    public TransactionType Type { get; private set; }
    public decimal Amount { get; private set; }
    public string? CreditAccount { get; private set; } = string.Empty;

    public static Transaction CreateCredit(string reference, string debitAccount, decimal amount, string creditAccount)
    {
        return new Transaction(reference, TransactionType.Credit, debitAccount, amount, creditAccount);
    }

    public static Transaction CreateDebit(string reference, string debitAccount, decimal amount, string creditAccount)
    {
        return new Transaction(reference, TransactionType.Debit, debitAccount, amount, creditAccount);
    }
}
