using CrudSystem.Application.DTOs;
using CrudSystem.Application.Interfaces;
using CrudSystem.Domain.Exceptions;

namespace CrudSystem.Api.Simulation;

public static class TransactionSimulator
{
    private const int RefreshEvery = 50;   // re-read funded accounts every N transfers

    public static async Task RunAsync(IServiceProvider services, int intervalMs)
    {
        var random = new Random();
        var accountNumbers = new List<string>();
        var iteration = 0;

        Console.WriteLine($"Transaction simulator running — one transfer every {intervalMs}ms. Ctrl+C to stop.");

        while (true)
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            var service = scope.ServiceProvider.GetRequiredService<ITransactionService>();

            // Refresh pool of funded accounts periodically, balances drift as transfer happens.
            if (iteration % RefreshEvery == 0)
            {
                accountNumbers = db.Accounts
                    .Where(a => a.Balance > 100)
                    .Select(a => a.AccountNumber)
                    .Take(50)
                    .ToList();

                if (accountNumbers.Count < 2)
                {
                    Console.WriteLine("Fewer than 2 funded accounts remain — stopping.");
                    return;
                }
            }
            iteration++;

            var debit = accountNumbers[random.Next(accountNumbers.Count)];
            var credit = accountNumbers[random.Next(accountNumbers.Count)];
            while (credit == debit) credit = accountNumbers[random.Next(accountNumbers.Count)];

            var amount = Math.Round((decimal)(random.NextDouble() * 50 + 1), 2);

            try
            {
                var result = await service.PostTransactionAsync(new PostTransactionRequest
                {
                    Amount = amount,
                    DebitAccount = debit,
                    CreditAccount = credit
                });
                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] posted {result.Reference}: {debit} -> {credit}  {amount}");
            }
            catch (DomainException ex)
            {
                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] skipped ({ex.Message})");
            }

            await Task.Delay(intervalMs);
        }
    }
}