using CrudSystem.Application.DTOs;

namespace CrudSystem.Application.Interfaces;

public interface ITransactionService
{
    Task<PostTransactionResponse> PostTransactionAsync(PostTransactionRequest request, CancellationToken cancellationToken = default);
}
