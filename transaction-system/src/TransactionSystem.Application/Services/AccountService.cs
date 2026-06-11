using Microsoft.EntityFrameworkCore;
using TransactionSystem.Application.DTOs;
using TransactionSystem.Application.Interfaces;
using TransactionSystem.Domain.Entities;
using TransactionSystem.Domain.Exceptions;

namespace TransactionSystem.Application.Services;

public class AccountService(IAppDbContext dbContext) : IAccountService
{
    private readonly IAppDbContext _dbContext = dbContext;

    public async Task<AccountResponse> CreateAccountAsync(CreateAccountRequest request, CancellationToken cancellationToken = default)
    {
        var accountNumber = await GenerateUniqueAccountNumberAsync(cancellationToken);

        var account = new Account(accountNumber, request.AccountName);

        await _dbContext.Accounts.AddAsync(account, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapAccount(account);
    }

    public async Task<IReadOnlyCollection<AccountResponse>> GetAccountsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Accounts
            .AsNoTracking()
            .OrderBy(x => x.AccountNumber)
            .Select(x => new AccountResponse
            {
                AccountNumber = x.AccountNumber,
                AccountName = x.AccountName,
                Balance = x.Balance,
                CreatedAt = x.CreatedAt,
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<AccountResponse?> GetAccountByAccountNumberAsync(string accountNumber, CancellationToken cancellationToken = default)
    {
        var account = await _dbContext.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.AccountNumber == accountNumber, cancellationToken);

        return account is null ? null : MapAccount(account);
    }

    public async Task<PostTransactionResponse> PostTransactionAsync(
        PostTransactionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.Equals(
                request.DebitAccount,
                request.CreditAccount,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException("Debit and credit accounts cannot be the same.");
        }

        var debitAccount = await _dbContext.Accounts
            .FirstOrDefaultAsync(x => x.AccountNumber == request.DebitAccount, cancellationToken) ?? throw new DomainException("Debit account not found.");

        var creditAccount = await _dbContext.Accounts
            .FirstOrDefaultAsync(x => x.AccountNumber == request.CreditAccount, cancellationToken) ?? throw new DomainException("Credit account not found.");

        var reference = GenerateTransactionReference();

        var debitEntry = debitAccount.Debit(reference, request.Amount, request.CreditAccount);

        var creditEntry = creditAccount.Credit(reference, request.Amount, request.DebitAccount);

        await _dbContext.Transactions.AddRangeAsync([debitEntry, creditEntry], cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new PostTransactionResponse
        {
            Reference = reference,
            DebitAccount = request.DebitAccount,
            CreditAccount = request.CreditAccount,
            Amount = request.Amount,
            CreatedAt = debitEntry.CreatedAt
        };
    }
    private async Task<string> GenerateUniqueAccountNumberAsync(CancellationToken cancellationToken)
    {
        string accountNumber;

        do
        {
            accountNumber = $"ACC{Random.Shared.Next(100000, 999999)}";
        }
        while (await _dbContext.Accounts
            .AnyAsync(x => x.AccountNumber == accountNumber, cancellationToken));

        return accountNumber;
    }

    private static string GenerateTransactionReference()
    {
        return $"TXN{DateTime.UtcNow:yyyyMMddHHmmssfff}{Random.Shared.Next(100, 999)}";
    }

    private static AccountResponse MapAccount(Account account)
    {
        return new AccountResponse
        {
            AccountNumber = account.AccountNumber,
            AccountName = account.AccountName,
            Balance = account.Balance,
            CreatedAt = account.CreatedAt,
        };
    }
}