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

// ---- Configure per run ----
const string mode = "revert";        // "inject" | "revert"
const string evalMode = "isolated";  // "isolated" | "compound"
const string fault = "ghostbal";         // for isolated: drop|duplicate|amount|ghosttxn|ghostbal
// ---------------------------

if (mode == "revert")
{
    // Revert ALL active faults (works for isolated single and compound).
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
        }
    }
    return;
}

// mode == "inject"
if (evalMode == "isolated")
{
    switch (fault)
    {
        case "drop": await drop.InjectAsync(); break;
        case "duplicate": await duplicate.InjectAsync(); break;
        case "amount": await amount.InjectAsync(); break;
        case "ghosttxn": await ghostTxn.InjectAsync(); break;
        case "ghostbal": await ghostBal.InjectAsync(); break;
    }
}
else if (evalMode == "compound")
{
    // One of each, random targets — tests masking/interaction.
    await drop.InjectAsync();
    await duplicate.InjectAsync();
    await amount.InjectAsync();
    await ghostTxn.InjectAsync();
    await ghostBal.InjectAsync();
    Console.WriteLine("Compound: all five faults injected.");
}