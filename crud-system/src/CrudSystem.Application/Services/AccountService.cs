using CrudSystem.Application.DTOs;
using CrudSystem.Application.Interfaces;
using CrudSystem.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CrudSystem.Application.Services;

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

    public async Task<AccountResponse?> GetAccountByAccountNumberAsync(string accountNumber, CancellationToken cancellationToken = default)
    {
        var account = await _dbContext.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.AccountNumber == accountNumber, cancellationToken);

        return account is null ? null : MapAccount(account);
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