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

namespace LifePunch.DXRP.Addons.StaffMenu;

/// <summary>
/// The ULX portal-parity catalogue, as data.
///
/// Every capability the DXRP portal grants an owner, classified by where it can actually live, with the
/// sensor each classification was read from. Reconciled 1:1 against the 38 <c>"Portal"</c>-category rows
/// of <c>game/Code/Api/Enums/Permission.cs</c> — the enum that file itself declares is mirrored from the
/// backend ("WARNING: ADD TO BACKEND NOT GAME, THIS GETS MIRRORED").
/// </summary>
/// <remarks>
/// <para>
/// WHY THIS IS SHIPPED RATHER THAN WRITTEN DOWN. The principal directive behind this lane is that an
/// owner should not need a browser open on the portal while they play. The honest first answer to that is
/// a map of what the game can and cannot reach — including the parts it cannot — so an owner learns the
/// boundary in-game instead of discovering it by finding an empty panel. A capability that is genuinely
/// portal-only is more useful shown and labelled than quietly omitted.
/// </para>
/// <para>
/// DENOMINATOR, NOT A SAMPLE, AND RECONCILED BY HAND. <see cref="All"/> holds all 38 Portal scopes, the
/// figure recorded in <see cref="ScopeCount"/>. It was reconciled against <c>Permission.cs</c> by hand on
/// 2026-08-29: 38 catalogue rows against 38 <c>"Portal"</c>-category rows — same ids, same order, no
/// duplicates and no strays, with <c>command.xray</c> correctly outside the denominator because it is
/// <c>"Commands"</c>-category, not <c>"Portal"</c>.
/// </para>
/// <para>
/// THIS CATALOGUE DOES NOT CHECK ITSELF. If the backend adds a scope and the mirrored enum grows, nothing
/// here notices — the next reader has to re-run that reconciliation by hand. Making it self-checking is
/// reachable in principle, because <c>PermissionExtensions.All</c> exposes each row's <c>Meta.Category</c>
/// without touching the mirrored file; but that reflection path has no other consumer in this tree and has
/// never been observed running in a live session. It is named here rather than shipped, because a sensor
/// whose own blindness reads exactly like the all-clear it reports is worse than an honest note.
/// </para>
/// <para>
/// BILLING IS PRINCIPAL DOMAIN. <c>portal.billing.manage</c> and the monetization and addon-purchase
/// scopes are catalogued and flagged via <see cref="LpParityCatalogRow.PrincipalDomain"/>. Nothing is
/// built against them and nothing is built near them. They are present so the map is complete, and for
/// no other reason.
/// </para>
/// </remarks>
public static class LpParityCatalog
{
	/// <summary>
	/// The number of <c>"Portal"</c>-category rows this catalogue was reconciled against, by hand, on
	/// 2026-08-29. A recorded figure, not a self-check — see the DENOMINATOR note on the type.
	/// </summary>
	public const int ScopeCount = 38;

	private static LpParityCatalogRow Read( string scope, string capability, string sensor, bool shipped = false )
		=> new( scope, capability, LpParityClass.InGameRead, shipped, false, sensor );

	private static LpParityCatalogRow Act( string scope, string capability, string sensor, bool shipped = false )
		=> new( scope, capability, LpParityClass.InGameAction, shipped, false, sensor );

	private static LpParityCatalogRow Portal( string scope, string capability, string sensor, bool principal = false )
		=> new( scope, capability, LpParityClass.PortalOnly, false, principal, sensor );

	/// <summary>
	/// The catalogue, in the same order the scopes appear in the mirrored <c>Permission</c> enum, so a
	/// reader can diff this list against that file top to bottom without reordering either.
	/// </summary>
	public static readonly IReadOnlyList<LpParityCatalogRow> All = new List<LpParityCatalogRow>
	{
		Portal( "portal.access", "Access the web portal",
			"Permission.cs — a gate on the browser, not a capability; the in-game menu opens for all and gates per row" ),

		Read( "portal.server.view", "View this server's identity and health",
			"ServerApiLink.TenantId/ServerId/RulesetId [Sync]; HostStatsTracker.GetStats (host); the multi-server LIST has no endpoint" ),

		Portal( "portal.server.edit", "Manage server settings",
			"UpdateServerInfoActionHandler is inbound only — no writer exists in ServerApiClient" ),

		Read( "portal.rank.view", "View ranks and what they grant",
			"RankSystem.GetPlayerRank returns a full RankDto incl. Permissions; the rank dictionary itself is private, so coverage is ranks held by online players" ),

		Portal( "portal.rank.edit", "Create and edit ranks",
			"no rank writer in ServerApiClient" ),

		Portal( "portal.map.view", "View map rotation",
			"only the current MapSboxIdentifier arrives at init and is passed straight to MapFitter.Fit; no rotation type exists" ),

		Portal( "portal.map.edit", "Manage map rotation",
			"as portal.map.view — nothing retained, nothing writable" ),

		Portal( "portal.ruleset.view", "View server rulesets",
			"only the bare RulesetId GUID reaches the game; there is no RulesetDto anywhere in Api/Dtos" ),

		Portal( "portal.ruleset.edit", "Manage rulesets",
			"as portal.ruleset.view" ),

		Read( "portal.faction.view", "View factions, roles and members",
			"ServerApiClient.GetAllFactions/GetFaction (host); FactionSystem already models FactionInfo/RoleInfo/MemberInfo" ),

		Read( "portal.sanction.view", "View a player's sanctions",
			"PlayerSanctionHistorySystem via StaffMenuHost.GetSanctions", shipped: true ),

		Portal( "portal.sanction.issue", "Issue sanctions from the portal",
			"portal.sanction.issue is distinct from the in-game player.* command permissions; no outbound writer is authorized by this portal scope" ),

		Portal( "portal.sanction.pardon", "Pardon or expire sanctions",
			"UpdateSanctionFlagsDto is defined and referenced NOWHERE in game code — no pardon writer exists" ),

		Read( "portal.audit.view", "View audit events",
			"SPLIT: LocalAuditStore is this host session's 500-row ring (shipped). The historical portal GET /v1/audit/events is unbound", shipped: true ),

		Portal( "portal.network.edit", "Manage tenant network settings",
			"no seam — the scope string occurs only in Permission.cs" ),

		Portal( "portal.announcement.edit", "Manage portal-persisted announcements",
			"the in-game /staffannounce command uses a separate command permission; this portal scope has no outbound writer" ),

		Read( "portal.items.view", "View items and inventories",
			"PlayerApiClient.GetInventory (client, own) and ServerApiClient.GetPlayerInventory (host, anyone); GetItemDefinition on both" ),

		Portal( "portal.items.manage", "Manage portal item definitions",
			"player inventory mutation is governed separately by inventory.manage; item-definition CRUD has no outbound writer" ),

		Read( "portal.addon.view", "View installed addons and revisions",
			"GameModeDto.Addons is [Sync]'d to clients with AddonName, AddonDescription, RevisionNumber, SboxVersion and Contents" ),

		Portal( "portal.addon.edit", "Manage addons and revisions",
			"inbound update_game_mode only" ),

		Portal( "portal.addon.entitlements.manage", "Grant addon access to other networks",
			"cross-tenant; no seam" ),

		Read( "portal.gamemode.view", "View the live game mode",
			"the whole GameModeDto is [Sync]'d: jobs, job groups, equipment, entities, market items, starting balance, building allowlists" ),

		Portal( "portal.gamemode.edit", "Manage game modes",
			"UpdateGameModeActionHandler is inbound; no outbound writer" ),

		Act( "portal.server.snapshot", "Save a server snapshot",
			"ServerApiClient.SaveSnapshot/GetSnapshot; SnapshotSystem.SaveSnapshot and SnapshotManualHost are public" ),

		Read( "portal.players.view", "View the player list",
			"StaffMenuHost.OnlinePlayers", shipped: true ),

		Read( "portal.players.view.incognito", "See incognito players in the list",
			"HasStatus(Constants.IncognitoStatus) — already an in-game scope by its own description" ),

		Read( "portal.player.view", "View a player's details",
			"StaffMenuHost.GetPlayerDetail — job, wallet, bank, health, armour, kills, deaths, playtime, rank", shipped: true ),

		Portal( "portal.player.view.alt", "View a player's alternate accounts",
			"hwid is sent OUTBOUND on the player pulse and never returned — PlayerPulseResponseDto has one field, Permitted" ),

		Portal( "portal.player.notes.manage", "Internal staff notes on players",
			"no seam — the scope string occurs only in Permission.cs" ),

		// HAZARD. Deliberately classified PORTAL-ONLY despite a public host-callable method existing.
		// RankSystem.SetPlayerRanks mutates the synced dictionary and NOTHING writes it back to the
		// portal, so an in-game assignment would appear to succeed and silently revert on the next
		// rank_snapshot or restart. Recorded here so a later lane does not rediscover it as easy work.
		Portal( "portal.rank.assign", "Assign ranks to players",
			"HAZARD: RankSystem.SetPlayerRanks mutates local synced state only; no portal writeback, so an in-game assign silently reverts" ),

		Read( "portal.store.manage", "Read and write the tenant data store",
			"ServerApiClient.ListStore/GetStore/SetStore/DeleteStore — already in production use here for waypoints and owner settings", shipped: true ),

		Portal( "portal.backups.manage", "View and restore network backups",
			"backup_restored is a notification the portal sends after the fact; listing and restoring have no writer" ),

		Portal( "portal.apikeys.manage", "Create, list and revoke API keys",
			"no seam — occurs only in Permission.cs; also a credential surface that does not belong in a game HUD" ),

		Portal( "portal.billing.manage", "Payment onboarding and billing status",
			"no billing seam of any kind exists in this tree — the string occurs only in Permission.cs", principal: true ),

		Portal( "portal.addon.purchase", "Purchase marketplace addons",
			"no seam; billing-adjacent", principal: true ),

		Portal( "portal.monetization.products.manage", "Create, edit and delete products",
			"no seam; billing-adjacent", principal: true ),

		Portal( "portal.monetization.coupons.manage", "Create, edit and delete coupons",
			"no seam; billing-adjacent", principal: true ),

		Portal( "portal.monetization.view", "Monetization breakdown and sales history",
			"no seam; billing-adjacent", principal: true )
	};

	/// <summary>Rows whose capability can live in-game today, in whole or in part.</summary>
	public static IEnumerable<LpParityCatalogRow> Feasible =>
		All.Where( row => row.Class != LpParityClass.PortalOnly );

	/// <summary>Rows the game cannot reach at all. Shown, not hidden — the boundary is the useful part.</summary>
	public static IEnumerable<LpParityCatalogRow> PortalOnly =>
		All.Where( row => row.Class == LpParityClass.PortalOnly );

	/// <summary>Rows already live in this addon.</summary>
	public static IEnumerable<LpParityCatalogRow> Shipped =>
		All.Where( row => row.Shipped );

	/// <summary>Short display label for a class, for a pill or column header.</summary>
	public static string ClassLabel( LpParityClass value ) => value switch
	{
		LpParityClass.InGameRead => "In-game read",
		LpParityClass.InGameAction => "In-game action",
		_ => "Portal only"
	};

	/// <summary>CSS tone suffix for a class pill, mirroring the audit pill convention already in the menu.</summary>
	public static string ClassTone( LpParityClass value ) => value switch
	{
		LpParityClass.InGameRead => "parity-pill-green",
		LpParityClass.InGameAction => "parity-pill-blue",
		_ => "parity-pill-slate"
	};
}
