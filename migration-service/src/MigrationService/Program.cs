using EventStore.Core;
using MigrationService.Backfill;
using MigrationService.Crud;

var builder = Host.CreateApplicationBuilder(args);
//builder.Services.AddHostedService<Worker>();
var config = builder.Configuration;

var crudConn = config.GetConnectionString("CrudDb")!;
var eventConn = config.GetConnectionString("EventsDb")!;

builder.Services.AddSingleton(new Reader(crudConn));
builder.Services.AddEventStore(eventConn);
builder.Services.AddSingleton<BackfillRunner>();

var host = builder.Build();
//host.Run();

// Run backfill
var runner = host.Services.GetRequiredService<BackfillRunner>();
await runner.RunAsync(CancellationToken.None);

Console.WriteLine("Backfill finished. Exiting.");

