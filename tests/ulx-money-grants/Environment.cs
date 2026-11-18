using Dxura.RP.Game;
using Sandbox;

namespace Sandbox
{
    internal static class Networking { public static bool IsHost = true; }
    internal sealed class Connection(long steamId)
    {
        public long SteamId { get; set; } = steamId;
        public Guid Id { get; set; } = Guid.NewGuid();
        public string DisplayName { get; set; } = "Staff";
        public bool IsActive { get; set; } = true;
        public bool IsHost { get; set; }
        public static List<Connection> All = [];
    }
    internal static class Rpc
    {
        internal sealed class HostAttribute : Attribute;
        public static Connection Caller = null!;
        public static List<Guid[]> Recipients = [];
        public static IDisposable FilterInclude(Func<Connection, bool> predicate)
        {
            Recipients.Add(Connection.All.Where(predicate).Select(c => c.Id).ToArray());
            return new Scope();
        }
        private sealed class Scope : IDisposable { public void Dispose() { } }
    }
    internal static class GameTask { public static Task MainThread() => Task.CompletedTask; }
    internal static class Log { public static void Warning(string message) { } }
    internal static class Validity { public static bool IsValid(this Player? player) => player?.Valid == true; }
}

namespace Dxura.RP.Shared
{
    internal sealed class InitalizePlayerDto { public long Id { get; set; } public string Name { get; set; } = ""; }
    internal sealed class InitalizePlayerResponseDto { public uint Balance { get; set; } }
    internal static class Permission { public const string ManageEconomy = "economy.manage"; }
}

namespace Dxura.RP.Game
{
    internal sealed class GameManager { public static bool HasAuthorizationKey = true; public static string? TestScope;
        public static GameManager Instance = new();
        public Guid Id = Guid.NewGuid(), ServerId = Guid.NewGuid();
        public string? TenantId => TestScope;
        public bool IsValid() => true; }
    internal static class ServerApiClient
    {
        public static uint RemoteBalance;
        public static Func<Dxura.RP.Shared.InitalizePlayerDto, Task<Dxura.RP.Shared.InitalizePlayerResponseDto?>> Read = null!;
        public static List<long> Reads = [];
        public static Task<Dxura.RP.Shared.InitalizePlayerResponseDto?> InitializePlayer(Dxura.RP.Shared.InitalizePlayerDto dto)
        {
            Reads.Add(dto.Id);
            return Read(dto);
        }
    }
    internal sealed class Config
    {
        public static Config Current = new();
        public GameSettings Game = new();
        public bool IsReady = true;
    }
    internal sealed class GameSettings { public bool MoneyEnabled = true; }
    internal static class RankSystem
    {
        public static bool Allowed = true, Targetable = true;
        public static bool HasPermission(long id, string permission) => Allowed && permission == "economy.manage";
        public static bool CanTarget(long caller, long target) => Targetable;
    }
    internal static class GameUtils
    {
        public static List<Player> Players = [];
        public static Player? GetPlayerByConnectionId(Guid id) => Players.FirstOrDefault(p => p.Connection?.Id == id);
    }
    internal sealed class Player(long steamId, Connection connection)
    {
        public long SteamId = steamId;
        public Connection? Connection = connection;
        public bool Valid = true;
        public static int Calls;
        public static List<(long Target, uint Amount, string Reason, bool Bank)> Payments = [];
        public static Func<Task<bool>> Outcome = () => Task.FromResult(true);
        // Read-only fixture balances make accidental addon-side balance assignments a compile failure.
        public uint WalletBalance => 100;
        public string DisplayName => Connection?.DisplayName ?? "";
        public uint TestBank = 200;
        public uint BankBalance => TestBank;
        public static bool ApplyToLocal, ApplyToRemote;
        public async Task<bool> PayHost(uint amount, string reason, bool inBank = false)
        {
            Calls++;
            Payments.Add((SteamId, amount, reason, inBank));
            var outcome = await Outcome();
            if (inBank && ApplyToLocal && outcome) TestBank += amount;
            if (inBank && ApplyToRemote && outcome) ServerApiClient.RemoteBalance += amount;
            return outcome;
        }
    }
}

namespace LifePunch.DXRP.Addons.StaffMenu
{
    internal static class StaffMenuHost
    {
        public static HashSet<long> Synthetic = [];
        public static bool IsSyntheticPlayer(long id) => Synthetic.Contains(id);
    }
    public sealed partial class StaffMenuBridgeService
    {
        internal record Result(Guid Request, string State, string Message);
        internal List<Result> Results = [];
        internal int Unresolved => _moneyGrantUnresolvedByCaller.Count;
        internal int InFlight => _moneyGrantInFlight.Count;
        private void ReceiveMoneyGrantResultClient(Guid request, bool succeeded, string message)
            => Results.Add(new(request, succeeded ? "Succeeded" : "Rejected", message));
        private void ReceiveMoneyGrantReconcileStateClient(Guid request, bool inFlight, string message)
            => Results.Add(new(request, inFlight ? "Pending" : "Unknown", message));
    }
}
