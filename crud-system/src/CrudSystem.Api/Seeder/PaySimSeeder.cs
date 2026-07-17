using CrudSystem.Domain.Entities;
using CrudSystem.Infrastructure.Persistence;

namespace CrudSystem.Api.Seeder;

public static class PaySimSeeder
{
    public static async Task SeedAsync(AppDbContext db, string csvPath, int count)
    {
        var transfers = File.ReadAllLines(csvPath).Skip(1).Take(count)
            .Select(l => l.Split(','))
            .Select(p => (Step: int.Parse(p[0]), Amount: decimal.Parse(p[1]),
                          Orig: p[2], Dest: p[3]))
            .ToList();

        var baseDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var accounts = new Dictionary<string, Account>();
        var net = new Dictionary<string, decimal>(); // net position per account
        var transactions = new List<(Transaction txn, DateTime createdAt)>();

        foreach (var (Step, Amount, Orig, Dest) in transfers)
        {
            if (!accounts.ContainsKey(Orig))
            {
                accounts[Orig] = new Account(Orig, $"Customer {Orig}");
                net[Orig] = 0;
            }
            if (!accounts.ContainsKey(Dest))
            {
                accounts[Dest] = new Account(Dest, $"Customer {Dest}");
                net[Dest] = 0;
            }

            net[Orig] -= Amount;
            net[Dest] += Amount;

            var reference = "TXN" + Guid.NewGuid().ToString("N")[..20].ToUpper();
            var createdAt = baseDate.AddHours(Step);

            transactions.Add((Transaction.CreateDebit(reference, Orig, Amount, Dest), createdAt));
            transactions.Add((Transaction.CreateCredit(reference, Orig, Amount, Dest), createdAt));
        }

        // Set account's private Balance and CreatedAt fields  via EF's tracker.
        foreach (var (number, account) in accounts)
        {
            db.Accounts.Add(account);
            db.Entry(account).Property("Balance").CurrentValue = net[number];
            db.Entry(account).Property("CreatedAt").CurrentValue = baseDate;
        }

        // Set transactions private CreatedAt fields via EF's tracker.
        foreach (var (txn, createdAt) in transactions)
        {
            db.Transactions.Add(txn);
            db.Entry(txn).Property("CreatedAt").CurrentValue = createdAt;
        }

        await db.SaveChangesAsync();
        Console.WriteLine($"Seeded {accounts.Count} accounts and {transactions.Count} rows ({transfers.Count} transfers).");
    }
}