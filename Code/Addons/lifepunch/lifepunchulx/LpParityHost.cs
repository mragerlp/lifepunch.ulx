// ─────────────────────────────────────────────────────────────────────────────
// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.
//
// "lifepunchulx" (s&box ident: lifepunch.lifepunchulx · addon ident: lifepunchulx) is the sole-owned
// intellectual property of lifepunch.co. It is NOT licensed for resale, redistribution,
// sublicensing, copying, or reuse by ANY person or entity — including DXRP and
// LifePunch staff, contributors, or community — EXCEPT the owner (lifepunch.co).
// Author account: mrragerlp · Public alias (in-game · Steam · Discord): Bloodwave
// Presence in this repository or on the DXRP portal grants no rights to anyone else.
// ─────────────────────────────────────────────────────────────────────────────

using System.Collections.Generic;
using System.Linq;
using Sandbox;
#if !LIFEPUNCH_LOCAL
using Dxura.RP.Game;
using Dxura.RP.Shared;
#endif

namespace LifePunch.DXRP.Addons.StaffMenu;

/// <summary>
/// Read-only portal-parity views for LIFEPUNCH ULX: the portal pages an owner would otherwise open a
/// browser for, rebuilt from data the game already holds.
/// </summary>
/// <remarks>
/// <para>
/// NO NEW TRANSPORT. Every value below is read from state that is ALREADY inside the game process — the
/// <c>[Sync]</c>'d <c>Config.GameMode</c>, the <c>[Sync]</c>'d <c>ServerApiLink</c> identity fields, the
/// synced rank dictionaries, the live roster. This class opens no socket, adds no RPC, invents no
/// endpoint and calls no <c>ServerApiClient</c> method. That is a hard constraint of this lane, and it is
/// also why the class is a pure projection: it is safe to call from a render path.
/// </para>
/// <para>
/// PROVENANCE IS PART OF THE VALUE. Every field carries an <see cref="LpParityAvailability"/>. The reason
/// is <c>GameNetworkManager.ServerName</c> and <c>MaxPlayers</c>: plain <c>static</c> fields with no
/// <c>[Sync]</c>, assigned only inside the host-only <c>ServerApiLink.Initialize()</c>. On a client they
/// still hold their compile-time defaults — "Unknown Server" and 128 — and those look exactly like
/// answers. Printing them to a client would be fabricating server identity. They are therefore reported
/// as <see cref="LpParityAvailability.HostOnly"/> unless this viewer really is the host.
/// </para>
/// <para>
/// LINK STATE IS READ THE WAY DXRP READS IT. <c>ServerApiLink.HasAuthorizationKey</c> reads the
/// <c>authorize</c> ConVar, which on a client is the CLIENT's own (empty) convar — so on every client it
/// answers "not linked" regardless of the truth. The client-safe signal is a non-empty synced
/// <c>TenantId</c>, which is exactly what DXRP's own UI tests (<c>PauseMenu.razor</c>,
/// <c>DashboardTabMenuSection.razor</c>). This follows that precedent rather than inventing a second one.
/// </para>
/// <para>
/// ONE THING IS DELIBERATELY NOT READ. <c>HostStatsTracker.GetStats()</c> is not a getter: its last act is
/// <c>_minFps = _fps</c>, resetting the 30-second minimum it exists to report. Calling it from a UI read
/// path would silently corrupt the very metric the portal pulse publishes. Host performance is therefore
/// reported as present-but-not-read, with the reason shown, instead of being sampled destructively.
/// </para>
/// </remarks>
#if LIFEPUNCH_PACKAGE
internal static class LpParityHost
{
	public const string RankViewPermissionId = "portal.rank.view";

	/// <summary>
	/// The current published parent predates the synced rank DTO surface used by the workbench.
	/// Fail closed instead of substituting fixtures or claiming that an empty list is a tenant roster.
	/// </summary>
	public static bool RanksAreAvailable => false;
	public static bool CanViewRanks() => StaffMenuHost.CanView( RankViewPermissionId );
	public static bool RanksAreComplete => false;
	public const string RanksScopeNote =
		"Rank observation is unavailable from the currently published parent package. No tenant-rank claim is made.";
	public static IReadOnlyList<LpParityRank> ObservedRanks() => System.Array.Empty<LpParityRank>();
}
#else
internal static class LpParityHost
{
	/// <summary>Positive code-string ID for this slice. Exists nowhere else in the tree.</summary>
	public const string ParityMark = "LP_ULX_PARITY_READ_20260828";

	// Portal scopes this surface mirrors. Hardcoded id strings for the same reason StaffMenuHost
	// hardcodes AuditPermissionId: the define-free editor build cannot see the Dxura enum.
	public const string ServerViewPermissionId = "portal.server.view";
	public const string GameModeViewPermissionId = "portal.gamemode.view";
	public const string RankViewPermissionId = "portal.rank.view";
	public const string AddonViewPermissionId = "portal.addon.view";

	/// <summary>UX gating only — mirrors <c>portal.server.view</c>.</summary>
	public static bool CanViewServer() => StaffMenuHost.CanView( ServerViewPermissionId );

	/// <summary>UX gating only — mirrors <c>portal.gamemode.view</c>.</summary>
	public static bool CanViewGameMode() => StaffMenuHost.CanView( GameModeViewPermissionId );

	/// <summary>UX gating only — mirrors <c>portal.rank.view</c>.</summary>
	public static bool CanViewRanks() => StaffMenuHost.CanView( RankViewPermissionId );

	/// <summary>UX gating only — mirrors <c>portal.addon.view</c>.</summary>
	public static bool CanViewAddons() => StaffMenuHost.CanView( AddonViewPermissionId );

	private const string HostOnlyText = "host only";
	private const string UnlinkedText = "not linked to a portal network";
	private const string PendingText = "waiting for the portal";
	private const string NoneText = "—";

	private static LpParityField Live( string label, string? value )
		=> new( label, string.IsNullOrWhiteSpace( value ) ? NoneText : value, LpParityAvailability.Live );

	private static LpParityField HostOnly( string label, string why = HostOnlyText )
		=> new( label, why, LpParityAvailability.HostOnly );

	private static LpParityField Unlinked( string label )
		=> new( label, UnlinkedText, LpParityAvailability.Unlinked );

	private static LpParityField Pending( string label )
		=> new( label, PendingText, LpParityAvailability.Pending );

	private static LpParitySection Section( string title, params LpParityField[] fields )
		=> new( title, fields );

	// --- Link state --------------------------------------------------------

	/// <summary>
	/// Whether this server is bound to a portal network, read the client-safe way (synced tenant id)
	/// rather than via the host-local <c>authorize</c> ConVar. See the class remarks.
	/// </summary>
	public static bool IsLinked
	{
#if LIFEPUNCH_LOCAL
		get => true;
#else
		get => !string.IsNullOrEmpty( ServerApiLink.Current?.TenantId );
#endif
	}

	/// <summary>True once the portal has answered and config overrides have been applied.</summary>
	public static bool IsReady
	{
#if LIFEPUNCH_LOCAL
		get => true;
#else
		get => Config.Current?.IsReady ?? false;
#endif
	}

	/// <summary>True when this viewer is the authoritative host and may see host-only values.</summary>
	public static bool IsHostViewer
	{
#if LIFEPUNCH_LOCAL
		get => true;
#else
		get => Networking.IsHost;
#endif
	}

	/// <summary>
	/// One-line state for a header chip: what this viewer can expect the parity views to contain.
	/// </summary>
	public static LpParityAvailability LinkState
	{
		get
		{
			if ( !IsLinked )
			{
				return LpParityAvailability.Unlinked;
			}

			return IsReady ? LpParityAvailability.Live : LpParityAvailability.Pending;
		}
	}

	/// <summary>
	/// Cheap change signal for a razor <c>BuildHash</c>. Folds the values that actually move — link and
	/// readiness, the roster size, and the identity of the live game mode — so a parity tab re-renders
	/// when the portal pushes a new game mode or a player joins, and stays still otherwise.
	/// </summary>
	public static int ParityVersion
	{
		get
		{
			var hash = IsLinked ? 17 : 3;
			hash = hash * 31 + ( IsReady ? 1 : 0 );
			hash = hash * 31 + PlayersOnline;
			hash = hash * 31 + GameModeName.GetHashCode();
			return hash;
		}
	}

	private static int PlayersOnline
	{
#if LIFEPUNCH_LOCAL
		get => 6;
#else
		get => GameUtils.Players.Count( player => player.IsValid() );
#endif
	}

	private static string GameModeName
	{
#if LIFEPUNCH_LOCAL
		get => "LIFEPUNCH RP (editor stub)";
#else
		get => Config.Current?.GameMode?.Name ?? "";
#endif
	}

	// --- Server status view (portal "Servers" page, this server only) ------

	/// <summary>
	/// The portal Servers page for THIS server: identity, link, and the parts of health the game can
	/// honestly see. Empty-state is explicit — an unlinked server returns the section with every field
	/// marked <see cref="LpParityAvailability.Unlinked"/> rather than an empty list, because "there is
	/// nothing to show and here is why" is the useful answer.
	/// </summary>
	public static IReadOnlyList<LpParitySection> ServerStatus()
	{
#if LIFEPUNCH_LOCAL
		return new List<LpParitySection>
		{
			Section( "Identity",
				Live( "Server", "LIFEPUNCH #1 (editor stub)" ),
				Live( "Tenant", "11111111-1111-1111-1111-111111111111" ),
				Live( "Server id", "00000000-0000-4000-8000-000000000001" ),
				Live( "Ruleset", "00000000-0000-4000-8000-0000000000ff" ) ),
			Section( "Link",
				Live( "Portal", "linked · production" ),
				Live( "Config", "ready" ),
				Live( "Pulse interval", "10s" ) ),
			Section( "Population",
				Live( "Players online", "6 / 128" ),
				Live( "Rank whitelist", "none" ) ),
			Section( "Host performance",
				HostOnly( "Reported to portal", "FPS and bandwidth are published each pulse; not sampled here" ) )
		};
#else
		var link = ServerApiLink.Current;
		var linked = IsLinked;

		var identity = new List<LpParityField>();
		if ( linked )
		{
			identity.Add( IsHostViewer
				? Live( "Server", GameNetworkManager.ServerName )
				: HostOnly( "Server" ) );
			identity.Add( Live( "Tenant", link?.TenantId ) );
			identity.Add( Live( "Server id", link is null || link.ServerId == System.Guid.Empty
				? ""
				: link.ServerId.ToString() ) );
			identity.Add( Live( "Ruleset", link?.RulesetId is null ? "" : link.RulesetId.Value.ToString() ) );
			identity.Add( Live( "Network",
				string.Equals( link?.TenantId, Constants.OfficialTenantId, System.StringComparison.OrdinalIgnoreCase )
					? "official"
					: "community" ) );
		}
		else
		{
			identity.Add( Unlinked( "Server" ) );
			identity.Add( Unlinked( "Tenant" ) );
		}

		var linkFields = new List<LpParityField>
		{
			linked
				? ( IsHostViewer ? Live( "Portal", $"linked · {EndpointLabel()}" ) : Live( "Portal", "linked" ) )
				: Unlinked( "Portal" ),
			linked
				? ( IsReady ? Live( "Config", "ready" ) : Pending( "Config" ) )
				: Unlinked( "Config" ),
			Live( "Pulse interval", $"{Constants.ApiServerSyncInterval}s" ),
			IsHostViewer
				? Live( "Portal address", Constants.BaseWebsiteUrl )
				: HostOnly( "Portal address" ),
			Live( "Build", Application.Version )
		};

		var population = new List<LpParityField>
		{
			IsHostViewer
				? Live( "Players online", $"{PlayersOnline} / {GameNetworkManager.MaxPlayers}" )
				: Live( "Players online", PlayersOnline.ToString() ),
			IsHostViewer
				? Live( "Max players", GameNetworkManager.MaxPlayers.ToString() )
				: HostOnly( "Max players" ),
			IsHostViewer
				? Live( "Rank whitelist", GameNetworkManager.WhitelistRankIds.Length == 0
					? "none"
					: $"{GameNetworkManager.WhitelistRankIds.Length} rank(s)" )
				: HostOnly( "Rank whitelist" )
		};

		// Deliberately NOT sampled. See the class remarks: GetStats() resets the 30-second minimum it
		// reports, so reading it from a render path would corrupt the portal's own metric.
		var performance = new List<LpParityField>
		{
			HostOnly( "Reported to portal", "FPS and bandwidth are published each pulse; not sampled here" ),
			IsHostViewer
				? Live( "Audit ring", $"{LocalAuditStore.SnapshotNewestFirst().Count} / {LocalAuditStore.Capacity} rows this session" )
				: HostOnly( "Audit ring" )
		};

		return new List<LpParitySection>
		{
			new( "Identity", identity ),
			new( "Link", linkFields ),
			new( "Population", population ),
			new( "Host performance", performance )
		};
#endif
	}

#if !LIFEPUNCH_LOCAL
	private static string EndpointLabel() => ServerApiLink.Endpoint switch
	{
		ApiEndpoint.Local => "local",
		ApiEndpoint.Staging => "staging",
		_ => "production"
	};
#endif

	// --- Game mode view (portal "Game Modes" + "Addons" pages) -------------

	/// <summary>
	/// The live game mode as the portal describes it. This is the richest genuinely-served surface in the
	/// tree: the whole <c>GameModeDto</c> is synced to clients, so jobs, equipment, entities, market items
	/// and the installed addon revisions are all readable without asking the host anything.
	/// </summary>
	public static IReadOnlyList<LpParitySection> GameModeOverview()
	{
#if LIFEPUNCH_LOCAL
		return new List<LpParitySection>
		{
			Section( "Game mode",
				Live( "Name", "LIFEPUNCH RP (editor stub)" ),
				Live( "Visibility", "Public" ),
				Live( "Starting balance", "$2,500" ) ),
			Section( "Content",
				Live( "Jobs", "24 in 5 groups" ),
				Live( "Equipment", "61" ),
				Live( "Entities", "38" ),
				Live( "Market items", "77" ) ),
			Section( "Addons",
				Live( "lifepunchulx", "rev 12 · sbox 1.0" ),
				Live( "serverhub", "rev 4 · sbox 1.0" ) )
		};
#else
		var mode = Config.Current?.GameMode;
		if ( mode is null || string.IsNullOrWhiteSpace( mode.Name ) || mode.Name == "None" )
		{
			// Honest empty-state: distinguish "no portal" from "portal has not answered yet".
			var why = IsLinked ? Pending( "Game mode" ) : Unlinked( "Game mode" );
			return new List<LpParitySection> { new( "Game mode", new List<LpParityField> { why } ) };
		}

		var summary = new List<LpParityField>
		{
			Live( "Name", mode.Name ),
			Live( "Description", mode.Description ),
			Live( "Visibility", mode.Visibility.ToString() ),
			Live( "Starting balance", mode.StartingBalance.ToString() ),
			Live( "Last modified", mode.LastModified.ToLocalTime().ToString( "yyyy-MM-dd HH:mm" ) )
		};

		// Read straight off the DTO rather than through GameModeEquipments.All / GameModeMarketItems.All:
		// those two helpers dereference Config.Current.GameMode without a null guard, so they throw in
		// exactly the pre-init window this view is most likely to be opened in.
		var jobs = mode.Jobs?.Count ?? 0;
		var groups = mode.JobGroups?.Count ?? 0;
		var content = new List<LpParityField>
		{
			Live( "Jobs", groups > 0 ? $"{jobs} in {groups} groups" : jobs.ToString() ),
			Live( "Equipment", ( mode.Equipments?.Count ?? 0 ).ToString() ),
			Live( "Entities", ( mode.Entities?.Count ?? 0 ).ToString() ),
			Live( "Market items", ( mode.MarketItems?.Count ?? 0 ).ToString() ),
			Live( "Building props", DescribeAllowlist( mode.BuildingProps?.Count ?? 0 ) ),
			Live( "Building materials", DescribeAllowlist( mode.BuildingMaterials?.Count ?? 0 ) )
		};

		var sections = new List<LpParitySection>
		{
			new( "Game mode", summary ),
			new( "Content", content )
		};

		if ( CanViewAddons() )
		{
			sections.Add( new LpParitySection( "Addons", AddonFields( mode ) ) );
		}

		return sections;
#endif
	}

#if !LIFEPUNCH_LOCAL
	/// <summary>An empty allowlist is not "zero props" — it means the legacy fallback rule applies.</summary>
	private static string DescribeAllowlist( int count )
		=> count == 0 ? "no explicit list · legacy fallback applies" : $"{count} explicitly allowed";

	private static IReadOnlyList<LpParityField> AddonFields( GameModeDto mode )
	{
		var addons = mode.Addons;
		if ( addons is null || addons.Count == 0 )
		{
			return new List<LpParityField> { Live( "Installed", "none" ) };
		}

		var fields = new List<LpParityField>( addons.Count );
		foreach ( var addon in addons.OrderBy( a => a.AddonName ?? "", System.StringComparer.OrdinalIgnoreCase ) )
		{
			var name = string.IsNullOrWhiteSpace( addon.AddonName ) ? addon.AddonId.ToString() : addon.AddonName;
			var detail = $"rev {addon.RevisionNumber}";
			if ( !string.IsNullOrWhiteSpace( addon.SboxVersion ) )
			{
				detail += $" · sbox {addon.SboxVersion}";
			}

			var contents = addon.Contents?.Count ?? 0;
			if ( contents > 0 )
			{
				detail += $" · {contents} content item(s)";
			}

			fields.Add( Live( name, detail ) );
		}

		return fields;
	}
#endif

	// --- Ranks view (portal "Ranks" page, observed subset) -----------------

	/// <summary>
	/// The ranks currently held by players online, with what each one actually grants.
	/// </summary>
	/// <remarks>
	/// PARTIAL, AND THE UI MUST SAY SO — see <see cref="RanksAreComplete"/>. <c>RankSystem</c>'s rank
	/// dictionary is <c>private</c>, so no addon can enumerate the tenant's full roster. What is reachable
	/// is <c>GetPlayerRank(steamId)</c>, which hands back a whole <c>RankDto</c> including its permission
	/// list. So this is a true statement about a real subset, not a truncated attempt at the whole, and it
	/// is labelled that way rather than being presented as the portal Ranks page.
	/// </remarks>
	public static IReadOnlyList<LpParityRank> ObservedRanks()
	{
#if LIFEPUNCH_LOCAL
		return new List<LpParityRank>
		{
			new( "Owner", 69, "#E74C3C", false, 1, 1, true ),
			new( "Super Admin", 10, "#3498DB", false, 74, 1, false ),
			new( "Admin", 5, "#2ECC71", false, 41, 1, false ),
			new( "Mod", 4, "#9B59B6", false, 18, 1, false ),
			new( "Member", 0, "#FFFFFF", true, 6, 2, false )
		};
#else
		var system = RankSystem.Instance;
		if ( !system.IsValid() )
		{
			return System.Array.Empty<LpParityRank>();
		}

		var byName = new Dictionary<string, LpParityRank>( System.StringComparer.OrdinalIgnoreCase );

		foreach ( var player in GameUtils.Players.Where( p => p.IsValid() ) )
		{
			var rank = system.GetPlayerRank( player.SteamId );
			if ( rank is null )
			{
				continue;
			}

			var name = SanitizeRankName( rank.Name );
			if ( string.IsNullOrWhiteSpace( name ) )
			{
				continue;
			}

			if ( byName.TryGetValue( name, out var existing ) )
			{
				byName[name] = existing with { HoldersOnline = existing.HoldersOnline + 1 };
				continue;
			}

			var permissions = rank.Permissions ?? new List<string>();
			byName[name] = new LpParityRank(
				name,
				rank.Order,
				$"#{rank.Color & 0xFFFFFFu:X6}",
				rank.IsDefault,
				permissions.Count,
				1,
				permissions.Contains( "*" ) );
		}

		return byName.Values
			.OrderByDescending( r => r.Order )
			.ThenBy( r => r.Name, System.StringComparer.OrdinalIgnoreCase )
			.ToList();
#endif
	}

	/// <summary>
	/// Always false on the live build: the rank roster cannot be enumerated from an addon, so this view is
	/// structurally a subset. Exposed so the razor can state the limit instead of implying completeness.
	/// </summary>
	public static bool RanksAreComplete => false;
	public static bool RanksAreAvailable => true;

	/// <summary>The sentence a parity Ranks panel should show under its heading.</summary>
	public const string RanksScopeNote =
		"Highest/display rank observed for players currently online. Secondary assignments and the full tenant roster are not readable from the game.";

#if !LIFEPUNCH_LOCAL
	/// <summary>
	/// Strips the leading colour/markup control characters DXRP stores in backend rank names.
	/// </summary>
	/// <remarks>
	/// This duplicates <c>StaffMenuHost.SanitizeRankName</c>, which is <c>private</c> and lives in a file
	/// this lane may not edit. The duplication is deliberate and disclosed rather than silently accepted:
	/// when the shell owner next touches <c>StaffMenuHost</c>, making that method <c>internal</c> and
	/// deleting this copy is a one-line collapse.
	/// </remarks>
	private static string SanitizeRankName( string raw )
	{
		if ( string.IsNullOrEmpty( raw ) )
		{
			return "";
		}

		var sb = new System.Text.StringBuilder( raw.Length );
		foreach ( var c in raw )
		{
			if ( char.IsControl( c ) || char.IsSurrogate( c ) )
			{
				continue;
			}

			var category = System.Globalization.CharUnicodeInfo.GetUnicodeCategory( c );
			if ( category is System.Globalization.UnicodeCategory.Format
			    or System.Globalization.UnicodeCategory.PrivateUse
			    or System.Globalization.UnicodeCategory.OtherNotAssigned )
			{
				continue;
			}

			sb.Append( c );
		}

		return sb.ToString().Trim();
	}
#endif
}
#endif
