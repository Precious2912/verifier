using VerificationService;
using VerificationService.Checkpoints;
using VerificationService.Invariants;
using VerificationService.Readers;

var builder = Host.CreateApplicationBuilder(args);
var config = builder.Configuration;

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

//Worker
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
await host.RunAsync();