using Dxura.RP.Game;
using Dxura.RP.Shared;
using LifePunch.DXRP.Addons.StaffMenu;
using Sandbox;
using System.Reflection;

void Check( bool condition, string description )
{
	if ( !condition ) throw new Exception( description );
	Console.WriteLine( description );
}

var request = typeof( StaffMenuBridgeService ).GetMethod( "RequestRanksHost" )!;
var response = typeof( StaffMenuBridgeService ).GetMethod( "ReceiveRankPage", BindingFlags.Instance | BindingFlags.NonPublic )!;
Check( request.IsDefined( typeof( Rpc.HostAttribute ) )
	&& response.GetCustomAttribute<Rpc.BroadcastAttribute>()?.Flags == (NetFlags.HostOnly | NetFlags.Reliable),
	"Only the host may originate a rank reply" );
Check( response.GetParameters().Select( p => p.ParameterType )
	.SequenceEqual( new[] { typeof( Guid ), typeof( int ), typeof( int ), typeof( bool ), typeof( string ) } ),
	"Rank reply uses engine-supported wire primitives" );

var system = RankSystem.Instance;
var current = system.Scene.Manager.ServerId;
var foreign = Guid.NewGuid();
var definitions = Enumerable.Range( 1, 35 ).Select( i => new RankDto
{
	Name = $"Rank {i}", Order = i, ServerIds = i % 2 == 0 ? [current] : []
} ).ToList();
var foreignRank = new RankDto { Name = "Other server only", ServerIds = [foreign] };
foreach ( var rank in definitions.Append( foreignRank ) ) system.Definitions.Add( rank.Id, rank );
GameUtils.Players.AddRange( [new Player( 123 ), new Player( 123 )] );
system.Assignments[123] = [definitions[0].Id, definitions[0].Id, definitions[1].Id];
var expected = definitions.OrderByDescending( d => d.Order ).Select( d =>
	new LpParityRank( d.Name, d.Order, "#123456", false, 0, d.Order <= 2 ? 1 : 0, false, d.Id ) ).ToArray();
var bridge = new StaffMenuBridgeService();
Rpc.Caller = Connection.Other;
StaffMenuHost.LocalGrant = true; // Simulates a modified client's local check.
bridge.RefreshRankDirectory( entered: true );
Check( TypeLibrary.Reads == 0 && Json.SentPages == 0, "Unauthorized caller never reads or serializes definitions" );
Check( bridge.RankReadState == RankDirectoryState.Denied && bridge.RankRows.Count == 0, "Denied client receives no rows" );
Check( Rpc.Recipients.Single().SequenceEqual( new[] { Connection.Other } ), "Denial targets the requesting connection" );
bridge.RequestRanksHost( Guid.NewGuid() );
Check( Rpc.Recipients.Count == 1, "Immediate repeat from denied caller is rate limited" );

RealTimeSince.Clock++;
Rpc.Caller = Connection.Host;
bridge.RefreshRankDirectory( entered: true );
Check( Json.SentPages == 3 && bridge.RankRows.SequenceEqual( expected ), "All 35 display rows survive ordered multi-page transport" );
Check( bridge.RankRows.All( r => r.Id != foreignRank.Id ), "Foreign-server definitions are excluded" );
Check( bridge.RankRows.Count( r => r.HoldersOnline == 1 ) == 2 && bridge.RankRows.Count( r => r.HoldersOnline == 0 ) == 33,
	"Distinct primary and secondary online assignments counted, including zero-holder ranks" );
Check( Rpc.Recipients.Last().SequenceEqual( new[] { Connection.Host } ), "Directory targets only the authorized connection" );

RealTimeSince.Clock++;
Connection.Host.IsActive = false; // Simulate a request with no response.
bridge.RefreshRankDirectory( entered: true );
var oldId = bridge.Pending;
Check( oldId != Guid.Empty && bridge.RankRows.Count == 0, "Opening Ranks clears old rows and correlates a fresh request" );
bridge.Deliver( Guid.NewGuid(), 0, true, expected[..1] );
Check( bridge.Pending == oldId && bridge.RankRows.Count == 0, "Unrelated response is discarded" );
bridge.Deliver( oldId, 0, false, expected[..16] );
Check( bridge.RankRows.Count == 0, "Incomplete pages remain hidden" );
StaffMenuHost.LocalGrant = false;
bridge.Deliver( oldId, 16, true, expected[16..20] );
Check( bridge.RankReadState == RankDirectoryState.Denied && bridge.RankRows.Count == 0, "Revocation clears an in-flight response" );

StaffMenuHost.LocalGrant = true;
bridge.RefreshRankDirectory( entered: true );
var lostId = bridge.Pending;
RealTimeSince.Clock += 13;
bridge.RefreshRankDirectory( entered: false );
Check( bridge.RankReadState == RankDirectoryState.Unavailable && bridge.Pending == Guid.Empty, "Timed-out request is unavailable" );
bridge.Deliver( lostId, 0, true, expected[..1] );
Check( bridge.RankRows.Count == 0 && bridge.RankReadState == RankDirectoryState.Unavailable, "Late answer cannot revive an expired request" );

Connection.Host.IsActive = true;
RealTimeSince.Clock += 5;
system.Definitions.Clear();
bridge.RefreshRankDirectory( entered: false );
Check( bridge.RankReadState == RankDirectoryState.Ready && bridge.RankRows.Count == 0, "An authoritative empty snapshot is ready" );
TypeLibrary.Sync.Flags = SyncFlags.None;
RealTimeSince.Clock += 5;
bridge.RefreshRankDirectory( entered: false );
Check( bridge.RankReadState == RankDirectoryState.Unavailable && bridge.RankRows.Count == 0, "Incompatible host synchronization metadata fails closed" );
