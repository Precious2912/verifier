using VerificationService.Invariants;
using VerificationService.Readers;
using VerificationService.Verdicts;

var builder = Host.CreateApplicationBuilder(args);
var config = builder.Configuration;

var crudConn = config.GetConnectionString("CrudDb")!;
var eventConn = config.GetConnectionString("EventsDb")!;

builder.Services.AddSingleton(new CrudReader(crudConn));
builder.Services.AddSingleton(new EventReader(eventConn));
builder.Services.AddSingleton(new CheckpointReader(eventConn));
builder.Services.AddSingleton<NumericInvariant>();
builder.Services.AddSingleton<RecordLevelInvariant>();

var host = builder.Build();
Console.WriteLine("VerificationService wired. Connections ready.");
var crud = host.Services.GetRequiredService<CrudReader>();
var events = host.Services.GetRequiredService<EventReader>();

// Basic test
// Numeric
var numeric = host.Services.GetRequiredService<NumericInvariant>();

// Record-level
var checkpointReader = host.Services.GetRequiredService<CheckpointReader>();
var recordInvariant = host.Services.GetRequiredService<RecordLevelInvariant>();

var accounts = await crud.GetAccountsAsync();
var transactions = await crud.GetTransactionsAsync();
var eventRows = await events.GetEventsAsync();

var cp = await checkpointReader.GetAsync();

// Run the checks
var numericVerdicts = numeric.Check(accounts, transactions, eventRows);
var recordVerdicts = recordInvariant.Check(transactions, eventRows, cp);


Console.WriteLine($"CRUD: {accounts.Count} accounts, {transactions.Count} transactions");
Console.WriteLine($"Events: {eventRows.Count} event rows");
Console.WriteLine($"Checkpoint: {(cp is null ? "none" : $"{cp.LastCreatedAt:o} / {cp.LastId}")}");

// Numeric Invariant Test
Console.WriteLine("----- Numeric Invariant Test -----");
foreach (var v in numericVerdicts)
    Console.WriteLine(
        $"{v.AccountNumber}: stored={v.StoredBalance}, crud={v.CrudDerivedBalance}, " +
        $"event={v.EventDerivedBalance} -> {v.Status}");


/* // Record-level Invariant Test
Console.WriteLine("----- Record-Level Invariant Test -----");
foreach (var v in recordVerdicts.Where(v => v.Status != RecordStatus.Matched))
    Console.WriteLine($"{v.Reference} [{v.Type}/{v.Account}]: " +
        $"crud={v.CrudAmount}, event={v.EventAmount} -> {v.Status}");

var matched = recordVerdicts.Count(v => v.Status == RecordStatus.Matched);
Console.WriteLine($"Matched: {matched}/{recordVerdicts.Count}"); */


//host.Run();
