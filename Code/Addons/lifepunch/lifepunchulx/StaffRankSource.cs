#if LIFEPUNCH_PACKAGE
using System;
using System.Collections.Generic;
using System.Linq;
using Dxura.RP.Game;
using Dxura.RP.Shared;
using Sandbox;

namespace LifePunch.DXRP.Addons.StaffMenu;

internal static class StaffRankSource
{
	// Called by the host after it authorizes the requesting connection.
	internal static bool TryRead( out IReadOnlyList<LpParityRank> rows )
	{
		rows = Array.Empty<LpParityRank>();
		if ( !Networking.IsHost || Config.Current?.IsReady != true )
			return false;

		var ranks = RankSystem.Instance;
		var serverId = ranks.IsValid() ? ranks.Scene.GetAllComponents<GameManager>().FirstOrDefault()?.ServerId : null;
		if ( serverId is not Guid currentServer || currentServer == Guid.Empty )
			return false;

		// Official DXRP syncs these definitions but has no public snapshot method.
		// Read only this typed, host-owned sync property via the engine's TypeLibrary.
		var property = TypeLibrary.GetType<RankSystem>()?.GetProperty( "Ranks" );
		if ( property is null || !property.CanRead ) return false;
		var sync = property.GetCustomAttribute<SyncAttribute>();
		if ( sync is null || (sync.Flags & SyncFlags.FromHost) == 0 ||
			property.GetValue( ranks ) is not NetDictionary<Guid, RankDto> definitions )
			return false;

		var holders = new Dictionary<Guid, int>();
		foreach ( var steamId in GameUtils.Players.Where( p => p.IsValid() ).Select( p => p.SteamId ).Distinct() )
		{
			foreach ( var id in ranks.GetPlayerRankIds( steamId ).Distinct() )
				holders[id] = holders.GetValueOrDefault( id ) + 1;
		}

		var result = new List<LpParityRank>( definitions.Count );
		foreach ( var (id, rank) in definitions )
		{
			if ( rank is null || id == Guid.Empty || id != rank.Id || rank.ServerIds is null )
				return false;
			if ( rank.ServerIds.Count > 0 && !rank.ServerIds.Contains( currentServer ) )
				continue;

			var name = StaffMenuHost.SanitizeRankName( rank.Name ).Replace( "<", "" ).Replace( ">", "" ).Trim();
			if ( name.Length > 128 ) name = name[..128];
			result.Add( new LpParityRank(
				string.IsNullOrWhiteSpace( name ) ? "Unnamed rank" : name,
				rank.Order, $"#{rank.Color & 0xFFFFFFu:X6}", rank.IsDefault,
				rank.Permissions?.Count ?? 0, holders.GetValueOrDefault( id ),
				rank.Permissions?.Contains( "*" ) == true, id ) );
		}

		rows = result.OrderByDescending( r => r.Order )
			.ThenBy( r => r.Name, StringComparer.OrdinalIgnoreCase ).ThenBy( r => r.Id ).ToList();
		return true;
	}
}
#endif
