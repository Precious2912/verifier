using Microsoft.EntityFrameworkCore;
using TransactionSystem.Domain.Entities;

namespace TransactionSystem.Application.Interfaces;

public interface IAppDbContext
{
    DbSet<Account> Accounts { get; }
    DbSet<Transaction> Transactions { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
