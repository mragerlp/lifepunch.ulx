#if LIFEPUNCH_PACKAGE
using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox;

namespace LifePunch.DXRP.Addons.StaffMenu;

internal enum RankDirectoryState { Loading, Ready, Denied, Unavailable }

public sealed partial class StaffMenuBridgeService
{
	private readonly Dictionary<long, RealTimeSince> _rankReadAge = new();
	private const int RankPageSize = 16;
	private const float RankRequestTimeout = 12f;
	private const float RankRequestCooldown = 0.5f;

	// These fields belong to this scene's bridge. Each client holds only its own answer.
	private RankDirectoryState _rankReadState = RankDirectoryState.Loading;
	internal RankDirectoryState RankReadState
	{
		get => _rankHostId == (Connection.Host?.Id ?? Guid.Empty) ? _rankReadState : RankDirectoryState.Loading;
		private set => _rankReadState = value;
	}
	internal IReadOnlyList<LpParityRank> RankRows { get; private set; } = Array.Empty<LpParityRank>();
	private readonly List<LpParityRank> _rankIncoming = new();
	private Guid _rankRequest;
	private Guid _rankHostId;
	private bool? _lastRankGrant;
	private RealTimeSince _rankAge;

	internal void RefreshRankDirectory( bool entered )
	{
		var hostId = Connection.Host?.Id ?? Guid.Empty;
		var grant = StaffMenuHost.CanView( LpParityHost.RankViewPermissionId );
		var changed = _lastRankGrant != grant || _rankHostId != hostId;
		if ( !changed && _rankRequest != Guid.Empty )
		{
			if ( _rankAge >= RankRequestTimeout )
			{
				RankRows = Array.Empty<LpParityRank>();
				RankReadState = grant ? RankDirectoryState.Unavailable : RankDirectoryState.Denied;
				_rankIncoming.Clear();
				_rankRequest = Guid.Empty;
				_rankAge = 0;
			}
			return;
		}
		if ( !changed && _rankAge < (entered ? RankRequestCooldown : 5f) ) return;

		if ( entered || changed || _rankRequest != Guid.Empty )
		{
			RankRows = Array.Empty<LpParityRank>();
			RankReadState = grant ? RankDirectoryState.Loading : RankDirectoryState.Denied;
		}
		_lastRankGrant = grant;
		_rankHostId = hostId;
		_rankIncoming.Clear();
		_rankRequest = Guid.NewGuid();
		_rankAge = 0;
		RequestRanksHost( _rankRequest );
	}

	[Rpc.Host]
	public void RequestRanksHost( Guid requestId )
	{
		var caller = Rpc.Caller;
		if ( requestId == Guid.Empty || !IsCurrentStaffConnection( caller ) )
			return;
		if ( _rankReadAge.TryGetValue( caller.SteamId, out var age ) )
		{
			if ( age < RankRequestCooldown ) return;
		}
		else
		{
			// Only verified connections can add keys. Forget expired entries as new players arrive.
			foreach ( var id in _rankReadAge.Where( entry => entry.Value >= RankRequestCooldown )
				.Select( entry => entry.Key ).ToArray() )
				_rankReadAge.Remove( id );
		}
		_rankReadAge[caller.SteamId] = 0;

		using ( Rpc.FilterInclude( c => (c.IsActive || c.IsHost) && c.Id == caller.Id && c.SteamId == caller.SteamId ) )
		{
			if ( !HasCurrentPermission( caller, LpParityHost.RankViewPermissionId ) )
			{
				ReceiveRankPage( requestId, (int)RankDirectoryState.Denied, 0, true, "" );
				return;
			}
			if ( !StaffRankSource.TryRead( out var rows ) )
			{
				ReceiveRankPage( requestId, (int)RankDirectoryState.Unavailable, 0, true, "" );
				return;
			}
			// A successful empty snapshot is distinct from a missing provider. Each page
			// contains display fields only, and is sent synchronously to the verified caller.
			for ( var offset = 0; ; offset += RankPageSize )
			{
				var last = rows.Count - offset <= RankPageSize;
				var json = Json.Serialize( rows.Skip( offset ).Take( RankPageSize ).ToArray() );
				ReceiveRankPage( requestId, (int)RankDirectoryState.Ready, offset, last, json );
				if ( last ) break;
			}
		}
	}

	[Rpc.Broadcast( NetFlags.HostOnly | NetFlags.Reliable )]
	private void ReceiveRankPage( Guid requestId, int wireState, int offset, bool last, string json )
	{
		if ( requestId == Guid.Empty || _rankRequest != requestId || _rankHostId != (Connection.Host?.Id ?? Guid.Empty) )
			return;

		var state = wireState switch
		{
			(int)RankDirectoryState.Ready => RankDirectoryState.Ready,
			(int)RankDirectoryState.Denied => RankDirectoryState.Denied,
			_ => RankDirectoryState.Unavailable
		};
		if ( !StaffMenuHost.CanView( LpParityHost.RankViewPermissionId ) )
		{
			_lastRankGrant = false;
			state = RankDirectoryState.Denied;
		}
		if ( state == RankDirectoryState.Ready && offset == _rankIncoming.Count )
		{
			var page = Json.Deserialize<LpParityRank[]>( json );
			if ( page is not null && page.Length <= RankPageSize && (last || page.Length == RankPageSize) )
			{
				_rankIncoming.AddRange( page );
				if ( !last ) return;
				RankRows = _rankIncoming.ToArray();
			}
			else state = RankDirectoryState.Unavailable;
		}
		else if ( state != RankDirectoryState.Denied )
			state = RankDirectoryState.Unavailable;

		if ( state != RankDirectoryState.Ready ) RankRows = Array.Empty<LpParityRank>();
		RankReadState = state;
		_rankIncoming.Clear();
		_rankRequest = Guid.Empty;
	}
}
#endif
