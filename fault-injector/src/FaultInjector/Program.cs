using FaultInjector;
using FaultInjector.GroundTruth;
using FaultInjector.Injectors;

var builder = Host.CreateApplicationBuilder(args);
var eventsConn = builder.Configuration.GetConnectionString("EventsDb")!;
var crudConn = builder.Configuration.GetConnectionString("CrudDb")!;

var log = new FaultLog(eventsConn);
await log.EnsureSchemaAsync();

var drop = new DroppedEvent(eventsConn, log);
var duplicate = new DuplicateEvent(eventsConn, log);
var amount = new IncorrectAmount(eventsConn, log);
var ghostTxn = new GhostTransaction(crudConn, log);
var ghostBal = new GhostBalance(crudConn, log);
var offsetting = new OffsettingFault(eventsConn, amount, log);
var combinedDropDuplicate = new CombinedDropDuplicate(eventsConn, drop, duplicate);

var mode = Args.GetValue(args, "--mode", "inject"); // inject | revert
var fault = Args.GetValue(args, "--fault", "drop"); // drop|duplicate|amount|ghosttxn|ghostbal|offsetting_amounts|combined_drop_duplicate
var scenario = Args.GetValue(args, "--scenario"); // optional override. Null unless batch run (e.g. batch_5);

if (mode == "revert")
{
    var active = await log.GetAllActiveAsync();
    if (active.Count == 0) { Console.WriteLine("No active faults."); return; }

    foreach (var f in active)
    {
        switch (f.FaultType)
        {
            case "DroppedEvent": await drop.RevertAsync(f); break;
            case "DuplicateEvent": await duplicate.RevertAsync(f); break;
            case "IncorrectAmount": await amount.RevertAsync(f); break;
            case "GhostTransaction": await ghostTxn.RevertAsync(f); break;
            case "GhostBalance": await ghostBal.RevertAsync(f); break;
            default: Console.WriteLine($"Unknown fault type '{f.FaultType}' — skipped."); break;
        }
    }
    Console.WriteLine($"Reverted {active.Count} fault(s).");
    return;
}

if (mode != "inject")
{
    Console.WriteLine($"Unknown mode '{mode}'. Use --mode inject|revert.");
    return;
}

switch (fault)
{
    case "drop": await drop.InjectAsync(scenario: scenario!); break;
    case "duplicate": await duplicate.InjectAsync(scenario: scenario!); break;
    case "amount": await amount.InjectAsync(scenario: scenario!); break;
    case "ghosttxn": await ghostTxn.InjectAsync(scenario: scenario!); break;
    case "ghostbal": await ghostBal.InjectAsync(scenario: scenario!); break;
    case "offsetting_amounts": await offsetting.InjectOffsettingAmountsAsync(); break;
    case "combined_drop_duplicate": await combinedDropDuplicate.InjectCombinedDropDuplicateAsync(); break;
    default:
        Console.WriteLine($"Unknown fault '{fault}'. Valid: drop, duplicate, amount, ghosttxn, ghostbal, offsetting_amounts, combined_drop_duplicate.");
        break;
}