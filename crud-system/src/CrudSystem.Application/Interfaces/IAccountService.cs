using CrudSystem.Application.DTOs;

namespace CrudSystem.Application.Interfaces;

public interface IAccountService
{
    Task<AccountResponse> CreateAccountAsync(CreateAccountRequest request, CancellationToken cancellationToken = default);

    Task<AccountResponse?> GetAccountByAccountNumberAsync(string accountNumber, CancellationToken cancellationToken = default);

    Task<PostTransactionResponse> PostTransactionAsync(PostTransactionRequest request, CancellationToken cancellationToken = default);
}
