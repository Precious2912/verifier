using Serilog;
using VerificationService;
using VerificationService.Checkpoints;
using VerificationService.Invariants;
using VerificationService.Readers;
using VerificationService.Verdicts;

var builder = Host.CreateApplicationBuilder(args);
var config = builder.Configuration;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: "logs/verification-.log",
        rollingInterval: RollingInterval.Day,
        outputTemplate:
        "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

builder.Services.AddSerilog();

//Connections
var crudConn = config.GetConnectionString("CrudDb")!;
var eventConn = config.GetConnectionString("EventsDb")!;
builder.Services.AddSingleton(new CrudReader(crudConn));
builder.Services.AddSingleton(new EventReader(eventConn));

//Readers & Checkpoints
builder.Services.AddSingleton(new MigrationCheckpointReader(eventConn));
builder.Services.AddSingleton(new VerificationCheckpointStore(eventConn));

//Invariants
builder.Services.AddSingleton<NumericInvariant>();
builder.Services.AddSingleton<RecordLevelInvariant>();
builder.Services.AddSingleton<SnapshotInvariant>();

//Scorer
builder.Services.AddSingleton(new DetectionScorer(eventConn));

//Worker
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
await host.RunAsync();