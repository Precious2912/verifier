using CrudSystem.Application.DTOs;
using CrudSystem.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CrudSystem.Api.Controllers;

[ApiController]
[Route("api/accounts")]
public class AccountsController(IAccountService service) : ControllerBase
{
    private readonly IAccountService _service = service;

    [HttpPost]
    public async Task<IActionResult> CreateAccount(
        CreateAccountRequest request, CancellationToken ct)
    {
        var account = await _service.CreateAccountAsync(request, ct);
        return Ok(ApiResponse<AccountResponse>.Success("Account created.", account));
    }

    [HttpGet("{accountNumber}")]
    public async Task<IActionResult> GetAccount(string accountNumber, CancellationToken ct)
    {
        var account = await _service.GetAccountByAccountNumberAsync(accountNumber, ct);
        return account is null
            ? NotFound(ApiResponse<AccountResponse>.Failure("Account not found."))
            : Ok(ApiResponse<AccountResponse>.Success("Account retrieved.", account));
    }
}