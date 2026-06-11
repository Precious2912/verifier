using CrudSystem.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CrudSystem.Application.Interfaces;

public interface IAppDbContext
{
    DbSet<Account> Accounts { get; }
    DbSet<Transaction> Transactions { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
