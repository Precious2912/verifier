using CrudSystem.Application.DTOs;
using CrudSystem.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CrudSystem.Api.Controllers;

[ApiController]
[Route("api/transactions")]
public class TransactionsController(IAccountService service) : ControllerBase
{
    private readonly IAccountService _service = service;

    [HttpPost]
    public async Task<IActionResult> Post(
        PostTransactionRequest request, CancellationToken ct)
    {
        var result = await _service.PostTransactionAsync(request, ct);
        return Ok(ApiResponse<PostTransactionResponse>.Success("Transaction posted.", result));
    }
}