using EventStore.Core;
using MigrationService;
using MigrationService.Backfill;
using MigrationService.Crud;
using MigrationService.Polling;

var builder = Host.CreateApplicationBuilder(args);
var config = builder.Configuration;

var crudConn = config.GetConnectionString("CrudDb")!;
var eventConn = config.GetConnectionString("EventsDb")!;

builder.Services.AddSingleton(new Reader(crudConn));
builder.Services.AddSingleton(new CheckpointStore(eventConn));
builder.Services.AddEventStore(eventConn);


var mode = Args.GetValue(args, "--mode", "sync");

if (mode == "sync")
{
    builder.Services.AddSingleton<SyncWorker>();
    builder.Services.AddHostedService(sp => sp.GetRequiredService<SyncWorker>());
    var host = builder.Build();
    Console.WriteLine("Sync mode: polling for new transactions. Ctrl+C to stop.");
    await host.RunAsync();
}
else if (mode == "backfill")
{
    builder.Services.AddSingleton<BackfillRunner>();
    var host = builder.Build();
    var runner = host.Services.GetRequiredService<BackfillRunner>();
    await runner.RunAsync(CancellationToken.None);
    Console.WriteLine("Backfill finished. Exiting.");
}
else
{
    Console.WriteLine("Unknown mode. Use --mode=backfill or --mode=sync.");
}

