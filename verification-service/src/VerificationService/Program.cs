using VerificationService.Readers;

var builder = Host.CreateApplicationBuilder(args);
var config = builder.Configuration;

var crudConn = config.GetConnectionString("CrudDb")!;
var eventConn = config.GetConnectionString("EventsDb")!;

builder.Services.AddSingleton(new CrudReader(crudConn));
builder.Services.AddSingleton(new EventReader(eventConn));
builder.Services.AddSingleton(new CheckpointReader(eventConn));


var host = builder.Build();

// Basic test
Console.WriteLine("VerificationService wired. Connections ready.");
var crud = host.Services.GetRequiredService<CrudReader>();
var events = host.Services.GetRequiredService<EventReader>();
var checkpoint = host.Services.GetRequiredService<CheckpointReader>();

var accounts = await crud.GetAccountsAsync();
var transactions = await crud.GetTransactionsAsync();
var eventRows = await events.GetEventsAsync();
var cp = await checkpoint.GetAsync();

Console.WriteLine($"CRUD: {accounts.Count} accounts, {transactions.Count} transactions");
Console.WriteLine($"Events: {eventRows.Count} event rows");
Console.WriteLine($"Checkpoint: {(cp is null ? "none" : $"{cp.LastCreatedAt:o} / {cp.LastId}")}");

//host.Run();
