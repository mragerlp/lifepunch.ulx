using System.Reflection;
using Dxura.RP.Game;
using LifePunch.DXRP.Addons.StaffMenu;
using Sandbox;

var checks = 0;
void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    checks++;
}
(StaffMenuBridgeService Bridge, Connection Caller, Player Target) Fixture()
{
    Networking.IsHost = true;
    GameManager.HasAuthorizationKey = true;
    Config.Current.Game.MoneyEnabled = true;
    RankSystem.Allowed = RankSystem.Targetable = true;
    StaffMenuHost.Synthetic.Clear();
    Player.Calls = 0; Player.Payments.Clear(); Player.Outcome = () => Task.FromResult(true);
    var caller = new Connection(101);
    var targetConnection = new Connection(202) { DisplayName = "Target" };
    Connection.All = [caller, targetConnection];
    Rpc.Caller = caller; Rpc.Recipients.Clear();
    var target = new Player(202, targetConnection);
    GameUtils.Players = [new Player(101, caller), target];
    return (new(), caller, target);
}
async Task Finish(StaffMenuBridgeService bridge)
{
    for (var i = 0; i < 100 && bridge.InFlight > 0; i++) await Task.Delay(5);
    Check(bridge.InFlight == 0, "Native adapter did not complete the fixture response");
}
void Rejected(Action<StaffMenuBridgeService, Connection, Player> setup, uint amount = 50, string reason = "Refund", bool bank = false)
{
    var (bridge, caller, target) = Fixture();
    setup(bridge, caller, target);
    bridge.GiveMoneyHost(Guid.NewGuid(), target.SteamId, amount, bank, reason);
    Check(Player.Calls == 0, "Invalid grant reached PayHost");
    Check(bridge.Unresolved == 0, "A rejected grant locked the caller");
}

var (bridge, caller, target) = Fixture();
var request = Guid.NewGuid();
bridge.GiveMoneyHost(request, target.SteamId, 50, false, "  Refund  ");
await Finish(bridge);
Check(Player.Calls == 1, "Authorized package cash grant must call native PayHost exactly once");
Check(Player.Payments.Single() == (target.SteamId, 50u, "Staff grant by Staff (101): Refund", false), "Wrong native cash payload");
Check(bridge.Results.Single().State == "Succeeded" && bridge.Results.Single().Message.Contains("locally")
      && bridge.Results.Single().Message.Contains("audit delivery is unconfirmed"), "Cash receipt overstated audit confirmation");
Check(bridge.Unresolved == 0, "Confirmed local cash should release its caller lock");
Check(Rpc.Recipients.Single().SequenceEqual(new[] { caller.Id }), "Grant result leaked to another client");
bridge.GiveMoneyHost(request, target.SteamId, 999, true, "Changed replay payload");
Check(Player.Calls == 1 && bridge.Results.Last().State == "Succeeded", "Replay performed another payment");
bridge.QueryMoneyGrantResultHost(request);
Check(Player.Calls == 1, "Query-only reconciliation paid again");
Check(typeof(StaffMenuBridgeService).GetMethod("GiveMoneyHost")!.IsDefined(typeof(Rpc.HostAttribute)), "Grant lost host RPC boundary");
Check(typeof(StaffMenuBridgeService).GetMethod("QueryMoneyGrantResultHost")!.IsDefined(typeof(Rpc.HostAttribute)), "Query lost host RPC boundary");
Check(StaffMoneyGrantCapabilities.IsDestinationAvailable("cash") && !StaffMoneyGrantCapabilities.IsDestinationAvailable("bank")
      && !StaffMoneyGrantCapabilities.IsDestinationAvailable("other"), "Package destination UI exposes an unavailable grant");
Check(StaffMoneyGrantCapabilities.ConfirmationNotice.Contains("unconfirmed")
      && !StaffMoneyGrantCapabilities.ConfirmationNotice.Contains("host audit ring"), "Package confirmation claims an unverified audit guarantee");
RankSystem.Allowed = false;
bridge.QueryMoneyGrantResultHost(request);
Check(bridge.Results.Last().State == "Succeeded" && Player.Calls == 1, "Permission loss must still allow own read-only receipt reconciliation");
Rpc.Caller = target.Connection!;
bridge.QueryMoneyGrantResultHost(request);
Check(bridge.Results.Last().State == "Unknown" && Player.Calls == 1, "Another caller retrieved the issuer's receipt or executed payment");
Check(Rpc.Recipients.Last().SequenceEqual(new[] { target.Connection!.Id }), "Query response leaked to the original issuer");

foreach (var invalidQuery in new Action[] {
    () => Networking.IsHost = false,
    () => Rpc.Caller = null!,
    () => Rpc.Caller.IsActive = false,
    () => Connection.All.Remove(Rpc.Caller),
    () => GameUtils.Players.RemoveAt(0),
    () => GameUtils.Players[0].Valid = false,
    () => GameUtils.Players[0].SteamId++,
    () => GameUtils.Players[0].Connection = new Connection(101) { Id = Rpc.Caller.Id },
})
{
    var queryCase = Fixture();
    invalidQuery();
    queryCase.Bridge.QueryMoneyGrantResultHost(Guid.NewGuid());
    Check(queryCase.Bridge.Results.Count == 0 && Rpc.Recipients.Count == 0 && Player.Calls == 0, "Invalid query caller reached receipt transport or native payment");
}
var emptyRequest = Fixture();
emptyRequest.Bridge.GiveMoneyHost(Guid.Empty, emptyRequest.Target.SteamId, 50, false, "Refund");
emptyRequest.Bridge.QueryMoneyGrantResultHost(Guid.Empty);
Check(Player.Calls == 0 && emptyRequest.Bridge.Results.Count == 0, "Empty request ID reached payment or reconciliation");

Rejected((_, _, _) => Networking.IsHost = false);
Rejected((_, _, _) => Rpc.Caller = null!);
Rejected((_, c, _) => c.IsActive = false);
Rejected((_, c, _) => Connection.All.Remove(c));
Rejected((_, _, _) => GameUtils.Players.RemoveAt(0));
Rejected((_, _, _) => GameUtils.Players[0].SteamId++);
Rejected((_, c, _) => GameUtils.Players[0].Connection = new Connection(101) { Id = c.Id });
Rejected((_, _, _) => GameUtils.Players[0].Valid = false);
Rejected((_, _, _) => RankSystem.Allowed = false);
Rejected((_, c, _) => StaffMenuHost.Synthetic.Add(c.SteamId));
Rejected((_, _, _) => GameManager.HasAuthorizationKey = false);
Rejected((_, _, _) => Config.Current.Game.MoneyEnabled = false);
Rejected((_, _, _) => { }, amount: 0);
Rejected((_, _, _) => { }, amount: (uint)int.MaxValue + 1);
Rejected((_, _, _) => { }, reason: "  ");
Rejected((_, _, t) => t.Valid = false);
Rejected((_, _, t) => t.Connection = null);
Rejected((_, _, t) => t.Connection!.IsActive = false);
Rejected((_, _, t) => Connection.All.Remove(t.Connection!));
Rejected((_, _, t) => t.Connection!.SteamId++);
Rejected((_, _, _) => RankSystem.Targetable = false);
Rejected((_, _, t) => StaffMenuHost.Synthetic.Add(t.SteamId));
Rejected((_, _, _) => { }, bank: true);
var bankCase = Fixture();
bankCase.Bridge.GiveMoneyHost(Guid.NewGuid(), bankCase.Target.SteamId, 50, true, "Refund");
Check(bankCase.Bridge.Results.Single().Message.Contains("Bank grants are unavailable"), "Bank rejection should explain native outcome limitation");

foreach (var failure in new[] { "false", "exception", "lost-link", "money-disabled", "target-left", "target-invalid", "connection-replaced", "host-lost" })
{
    var pending = Fixture();
    var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    Player.Outcome = () => completion.Task;
    var id = Guid.NewGuid();
    pending.Bridge.GiveMoneyHost(id, pending.Target.SteamId, 50, false, "Refund");
    Check(Player.Calls == 1 && pending.Bridge.Unresolved == 1 && pending.Bridge.InFlight == 1, "Pending grant must own a lock");
    pending.Bridge.GiveMoneyHost(id, pending.Target.SteamId, 50, false, "Refund");
    pending.Bridge.GiveMoneyHost(Guid.NewGuid(), pending.Target.SteamId, 50, false, "Second grant");
    Check(Player.Calls == 1, "In-flight repeat/new request called the native rail again");
    if (failure == "lost-link") GameManager.HasAuthorizationKey = false;
    if (failure == "money-disabled") Config.Current.Game.MoneyEnabled = false;
    if (failure == "target-left") pending.Target.Connection!.IsActive = false;
    if (failure == "target-invalid") pending.Target.Valid = false;
    if (failure == "connection-replaced") pending.Target.Connection = new Connection(pending.Target.SteamId);
    if (failure == "host-lost") Networking.IsHost = false;
    if (failure == "exception") completion.SetException(new InvalidOperationException("Uncertain native outcome"));
    else completion.SetResult(failure != "false");
    await Finish(pending.Bridge);
    Check(pending.Bridge.Results.Last().State == "Unknown" && pending.Bridge.Unresolved == 1, "Indeterminate outcome released caller or claimed rejection/success");
    Networking.IsHost = true;
    pending.Bridge.QueryMoneyGrantResultHost(id);
    pending.Bridge.GiveMoneyHost(id, pending.Target.SteamId, 50, false, "Replay");
    pending.Bridge.GiveMoneyHost(Guid.NewGuid(), pending.Target.SteamId, 50, false, "Retry");
    Check(Player.Calls == 1 && pending.Bridge.Unresolved == 1, "Unknown result allowed another native payment");
    GameManager.HasAuthorizationKey = true; Config.Current.Game.MoneyEnabled = true;
    pending.Target.Connection!.IsActive = true;
    for (var i = 0; i < 140; i++) pending.Bridge.GiveMoneyHost(Guid.NewGuid(), pending.Target.SteamId, 50, false, "Rejected while unknown");
    pending.Bridge.GiveMoneyHost(id, pending.Target.SteamId, 50, false, "Evicted original replay");
    Check(Player.Calls == 1 && pending.Bridge.Results.Last().State == "Unknown", "Receipt eviction lost unresolved lock");
}
Console.WriteLine($"PASS: {checks} package cash-grant assertions; actual host RPC, native call count, caller/rank/input guards, bank rejection, replay and unknown-outcome lock retention.");
