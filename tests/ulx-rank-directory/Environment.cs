using System.Text.Json;
using Dxura.RP.Game;
using LifePunch.DXRP.Addons.StaffMenu;

namespace Sandbox
{
	internal static class Networking { public static bool IsHost = true; }
	internal enum SyncFlags { None, FromHost }
	internal sealed class SyncAttribute : Attribute { public SyncFlags Flags = SyncFlags.FromHost; }
	internal sealed class NetDictionary<TKey, TValue> : Dictionary<TKey, TValue> where TKey : notnull;

	// The engine is outside this test. Only the mounted Ranks property's shape is simulated.
	internal sealed class TypeLibrary
	{
		internal static int Reads;
		internal static SyncAttribute Sync = new();
		public static TypeLibrary GetType<T>() => new();
		public TypeLibrary? GetProperty( string name ) => name == "Ranks" ? this : null;
		public bool CanRead => true;
		public T? GetCustomAttribute<T>() where T : Attribute => Sync as T;
		public object GetValue( RankSystem ranks ) { Reads++; return ranks.Definitions; }
	}

	internal sealed class Connection( long steamId )
	{
		internal long SteamId { get; } = steamId;
		internal Guid Id { get; } = Guid.NewGuid();
		internal bool IsActive { get; set; } = true;
		internal bool IsHost => this == Host;
		internal static Connection Host { get; } = new( 1 );
		internal static Connection Other { get; } = new( 2 );
	}

	internal readonly record struct RealTimeSince( float Started )
	{
		internal static float Clock { get; set; }
		public static implicit operator RealTimeSince( float age ) => new( Clock - age );
		public static implicit operator float( RealTimeSince value ) => Clock - value.Started;
	}

	[Flags] internal enum NetFlags { HostOnly = 1, Reliable = 2 }
	internal static class Rpc
	{
		internal sealed class HostAttribute : Attribute;
		internal sealed class BroadcastAttribute( NetFlags flags ) : Attribute { public NetFlags Flags => flags; }
		internal static Connection Caller = Connection.Host;
		internal static List<Connection[]> Recipients = new();
		internal static IDisposable FilterInclude( Func<Connection, bool> select )
		{
			Recipients.Add( new[] { Connection.Host, Connection.Other }.Where( select ).ToArray() );
			return new Scope();
		}
		private sealed class Scope : IDisposable { public void Dispose() { } }
	}

	internal static class Json
	{
		internal static int SentPages;
		public static string Serialize<T>( T value ) { SentPages++; return JsonSerializer.Serialize( value ); }
		public static T? Deserialize<T>( string value ) => JsonSerializer.Deserialize<T>( value );
	}
}

namespace LifePunch.DXRP.Addons.StaffMenu
{
	using Sandbox;

	internal static class LpParityHost { internal const string RankViewPermissionId = "portal.rank.view"; }
	internal static class StaffMenuHost
	{
		internal static bool LocalGrant;
		internal static bool CanView( string permission ) => LocalGrant && permission == LpParityHost.RankViewPermissionId;
		internal static string SanitizeRankName( string name ) => name;
	}

	public sealed partial class StaffMenuBridgeService
	{
		// Test substitutes for DXRP's player/connection lookups. Only the host identity has the grant.
		private static bool IsCurrentStaffConnection( Connection c ) => c.IsActive;
		private static bool HasCurrentPermission( Connection c, string p )
			=> IsCurrentStaffConnection( c ) && c == Connection.Host && p == LpParityHost.RankViewPermissionId;
		internal Guid Pending => _rankRequest;
		internal void Deliver( Guid id, int offset, bool last, LpParityRank[] data )
			=> ReceiveRankPage( id, (int)RankDirectoryState.Ready, offset, last, JsonSerializer.Serialize( data ) );
	}
}

namespace Dxura.RP.Shared
{
	internal sealed class RankDto
	{
		public Guid Id = Guid.NewGuid();
		public string Name = "";
		public int Order;
		public uint Color = 0x123456;
		public bool IsDefault { get; init; }
		public List<Guid>? ServerIds = [];
		public List<string> Permissions = [];
	}
}

namespace Dxura.RP.Game
{
	using Dxura.RP.Shared;
	using Sandbox;
	internal sealed class Config
	{
		public static Config Current = new();
		public bool IsReady = true;
	}
	internal sealed class GameManager { public Guid ServerId = Guid.NewGuid(); }
	internal sealed class TestScene
	{
		public GameManager Manager = new();
		public IEnumerable<T> GetAllComponents<T>() => new[] { Manager }.OfType<T>();
	}
	internal sealed class Player( long steamId )
	{
		public long SteamId => steamId;
		public bool IsValid() => true;
	}
	internal static class GameUtils { public static List<Player> Players = []; }
	internal sealed class RankSystem
	{
		public static RankSystem Instance = new();
		public TestScene Scene = new();
		public NetDictionary<Guid, RankDto> Definitions = new();
		public Dictionary<long, List<Guid>> Assignments = new();
		public bool IsValid() => true;
		public List<Guid> GetPlayerRankIds( long steamId ) => Assignments[steamId];
	}
}
