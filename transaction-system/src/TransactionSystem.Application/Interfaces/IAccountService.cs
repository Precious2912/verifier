using TransactionSystem.Application.DTOs;

namespace TransactionSystem.Application.Interfaces;

public interface IAccountService
{
    Task<AccountResponse> CreateAccountAsync(CreateAccountRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<AccountResponse>> GetAccountsAsync(CancellationToken cancellationToken = default);

    Task<AccountResponse?> GetAccountByAccountNumberAsync(string accountNumber, CancellationToken cancellationToken = default);

    Task<PostTransactionResponse> PostTransactionAsync(PostTransactionRequest request, CancellationToken cancellationToken = default);
}
