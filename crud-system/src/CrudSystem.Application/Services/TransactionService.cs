using CrudSystem.Application.DTOs;
using CrudSystem.Application.Interfaces;
using CrudSystem.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace CrudSystem.Application.Services;

public class TransactionService(IAppDbContext dbContext) : ITransactionService
{
    private readonly IAppDbContext _dbContext = dbContext;
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

    private static string GenerateTransactionReference()
    {
        return $"TXN{DateTime.UtcNow:yyyyMMddHHmmssfff}{Guid.NewGuid():N}"[..28].ToUpper();
    }
}