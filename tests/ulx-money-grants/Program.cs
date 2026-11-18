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
    GameManager.TestScope = "manager|tenant|server|endpoint";
    Config.Current.IsReady = true;
    ServerApiClient.Reads.Clear(); ServerApiClient.RemoteBalance = 200;
    ServerApiClient.Read = _ => Task.FromResult<Dxura.RP.Shared.InitalizePlayerResponseDto?>(new() { Balance = ServerApiClient.RemoteBalance });
    Player.ApplyToLocal = Player.ApplyToRemote = true;
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
Check(StaffMoneyGrantCapabilities.IsDestinationAvailable("cash") && StaffMoneyGrantCapabilities.IsDestinationAvailable("bank")
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
var bankCase = Fixture();
var bankRequest = Guid.NewGuid();
bankCase.Bridge.GiveMoneyHost(bankRequest, bankCase.Target.SteamId, 50, true, "Refund");
await Finish(bankCase.Bridge);
Check(Player.Calls == 1 && Player.Payments.Single().Bank && Player.Payments.Single().Reason.Contains(bankRequest.ToString("N")), "Bank payment must carry its request reference and run once");
Check(ServerApiClient.Reads.SequenceEqual(new long[] { 202, 202 }) && bankCase.Target.BankBalance == 250 && ServerApiClient.RemoteBalance == 250, "Read authoritative before/after balances from the correct account");
Check(bankCase.Bridge.Results.Single().State == "Succeeded" && bankCase.Bridge.Results.Single().Message.Contains("250")
      && bankCase.Bridge.Results.Single().Message.Contains("receipt", StringComparison.OrdinalIgnoreCase), "Successful bank result must report the verified balance and receipt limitation");
bankCase.Bridge.QueryMoneyGrantResultHost(bankRequest);
bankCase.Bridge.GiveMoneyHost(bankRequest, 202, 50, true, "Replay");
Check(Player.Calls == 1 && ServerApiClient.Reads.Count == 2 && bankCase.Bridge.Unresolved == 0, "Positive receipt and replay cannot pay twice");

Check(StaffMoneyGrantCapabilities.ConfirmationFor("bank").Contains("before and after")
      && StaffMoneyGrantCapabilities.ConfirmationFor("bank").Contains("does not issue a transaction receipt")
      && StaffMoneyGrantCapabilities.ConfirmationFor("cash") == StaffMoneyGrantCapabilities.ConfirmationNotice,
      "Each destination needs its own accurate confirmation");
var selfGrant = Fixture();
selfGrant.Bridge.GiveMoneyHost(Guid.NewGuid(), selfGrant.Caller.SteamId, 50, true, "Owner self-grant");
await Finish(selfGrant.Bridge);
Check(Player.Calls == 1 && Player.Payments.Single().Target == selfGrant.Caller.SteamId
      && ServerApiClient.Reads.SequenceEqual(new[] { selfGrant.Caller.SteamId, selfGrant.Caller.SteamId })
      && selfGrant.Bridge.Results.Single().State == "Succeeded", "Self-grant must verify the issuer's own account");

foreach (var invalid in new[] { "scope-missing", "config-unready", "read-failure", "null", "different", "overflow", "permission-changed", "hierarchy-changed", "scope-changed", "issuer-left", "target-left", "money-disabled", "target-mirror-changed" })
{
    var x = Fixture();
    if (invalid == "scope-missing") GameManager.TestScope = null;
    if (invalid == "config-unready") Config.Current.IsReady = false;
    if (invalid == "overflow") x.Target.TestBank = ServerApiClient.RemoteBalance = uint.MaxValue - 3;
    ServerApiClient.Read = _ => {
        if (invalid == "read-failure") throw new Exception("fixture preflight network failure");
        if (invalid == "permission-changed") RankSystem.Allowed = false;
        if (invalid == "hierarchy-changed") RankSystem.Targetable = false;
        if (invalid == "scope-changed") GameManager.TestScope = "other";
        if (invalid == "issuer-left") x.Caller.IsActive = false;
        if (invalid == "target-left") x.Target.Connection!.IsActive = false;
        if (invalid == "money-disabled") Config.Current.Game.MoneyEnabled = false;
        if (invalid == "target-mirror-changed") x.Target.TestBank++;
        return Task.FromResult<Dxura.RP.Shared.InitalizePlayerResponseDto?>(invalid == "null" ? null : new() { Balance = ServerApiClient.RemoteBalance + (invalid == "different" ? 1u : 0u) });
    };
    x.Bridge.GiveMoneyHost(Guid.NewGuid(), 202, 50, true, "Refund");
    await Finish(x.Bridge);
    Check(Player.Calls == 0 && x.Bridge.Unresolved == 0 && !x.Bridge.Results.Any(r => r.State == "Succeeded"), "Rejected bank preflight changed money or retained a lock: " + invalid);
}
foreach (var issue in new[] { "service-delta", "local-delta", "native-false", "native-throws", "scope-lost", "post-read-fails", "post-null", "target-replaced" })
{
    var x = Fixture(); var original = Guid.NewGuid();
    Player.ApplyToRemote = issue != "service-delta"; Player.ApplyToLocal = issue != "local-delta";
    Player.Outcome = () => issue == "native-throws" ? throw new Exception("fixture lost acknowledgement") : Task.FromResult(issue != "native-false");
    ServerApiClient.Read = _ => {
        if (ServerApiClient.Reads.Count == 2) {
            if (issue == "scope-lost") GameManager.TestScope = "different";
            if (issue == "post-read-fails") throw new Exception("fixture after-read failure");
            if (issue == "post-null") return Task.FromResult<Dxura.RP.Shared.InitalizePlayerResponseDto?>(null);
            if (issue == "target-replaced") x.Target.Connection = new Connection(202);
        }
        return Task.FromResult<Dxura.RP.Shared.InitalizePlayerResponseDto?>(new() { Balance = ServerApiClient.RemoteBalance });
    };
    x.Bridge.GiveMoneyHost(original, 202, 50, true, "Uncertain grant"); await Finish(x.Bridge);
    Check(Player.Calls == 1 && x.Bridge.Results.Last().State == "Unknown" && x.Bridge.Unresolved == 1, "Uncertain bank operation must lock and remain UNKNOWN: " + issue);
    if (issue is not ("target-replaced" or "scope-lost")) {
        ServerApiClient.RemoteBalance = 250; // An unrelated credit could match the requested amount.
        ServerApiClient.Read = _ => Task.FromResult<Dxura.RP.Shared.InitalizePlayerResponseDto?>(new() { Balance = 250 });
        var reads = ServerApiClient.Reads.Count;
        x.Bridge.QueryMoneyGrantResultHost(original);
        Check(ServerApiClient.Reads.Count == reads + 1 && x.Bridge.Results.Last().State == "Unknown"
              && x.Bridge.Results.Last().Message.Contains("250"), "Authorized recheck reads current balance and cannot confirm attribution: " + issue);
    }
    var other = new Connection(303); Connection.All.Add(other); GameUtils.Players.Add(new Player(303, other)); Rpc.Caller = other;
    x.Bridge.GiveMoneyHost(Guid.NewGuid(), 202, 50, false, "Another issuer");
    Check(Player.Calls == 1, "A second issuer used an unresolved target: " + issue);
    Rpc.Caller = x.Caller; x.Bridge.GiveMoneyHost(original, 202, 50, true, "Replay");
    x.Bridge.GiveMoneyHost(Guid.NewGuid(), 303, 50, true, "Second target");
    Check(Player.Calls == 1 && x.Bridge.Unresolved == 1, "Unknown bank grant repeated or released its lock: " + issue);
}

var busy = Fixture(); var gated = new TaskCompletionSource<Dxura.RP.Shared.InitalizePlayerResponseDto?>();
ServerApiClient.Read = _ => gated.Task;
var busyRequest = Guid.NewGuid(); busy.Bridge.GiveMoneyHost(busyRequest, 202, 50, true, "Pending preflight");
var staff2 = new Connection(303); Connection.All.Add(staff2); GameUtils.Players.Add(new Player(303, staff2));
Rpc.Caller = staff2; busy.Bridge.GiveMoneyHost(Guid.NewGuid(), 202, 50, false, "Other staff");
Check(Player.Calls == 0 && ServerApiClient.Reads.Count == 1, "Pending preflight must own the target lock");
Rpc.Caller = busy.Caller; busy.Bridge.GiveMoneyHost(busyRequest, 202, 50, true, "Replay");busy.Bridge.QueryMoneyGrantResultHost(busyRequest);
Check(ServerApiClient.Reads.Count == 1 && busy.Bridge.Results.Last().State == "Pending", "Recheck or replay repeated pending preflight");
ServerApiClient.Read = _ => Task.FromResult<Dxura.RP.Shared.InitalizePlayerResponseDto?>(new() { Balance = ServerApiClient.RemoteBalance });
gated.SetResult(new() { Balance = 200 }); await Finish(busy.Bridge);
Rpc.Caller = staff2; busy.Bridge.GiveMoneyHost(Guid.NewGuid(), 202, 20, false, "After verified bank");
Check(Player.Calls == 2 && busy.Bridge.Unresolved == 0, "Verified bank grant should free both locks");

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
Console.WriteLine($"PASS: {checks} native money-grant assertions covering cash, authenticated bank balance checks, caller and target locks, and read-only reconciliation.");
